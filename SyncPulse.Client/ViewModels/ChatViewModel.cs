using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using SyncPulse.Client.Models;
using SyncPulse.Client.Services;
using SyncPulse.Client.Utils;
using SyncPulse.Core.Enums;
using SyncPulse.Core.Packets;
using SyncPulse.Core.Protocol;

namespace SyncPulse.Client.ViewModels
{
    public class ChatViewModel : INotifyPropertyChanged
    {
        private readonly ClientNetworkService _network;
        private ContactItem? _selectedContact;
        private string _searchQuery = string.Empty;
        private string _searchResultText = string.Empty;
        private bool _isSearching;
        private string _inputMessage = string.Empty;
        private string _systemAnnouncement = string.Empty;
        private bool _hasSystemAnnouncement;
        private bool _isCallHistoryOpen;
        private DateTime _lastTypingSentTime = DateTime.MinValue;
        private readonly ConcurrentDictionary<int, DispatcherTimer> _typingTimers = new();
        private readonly string _downloadsDir;

        public ObservableCollection<ContactItem> Contacts { get; } = new();
        public ObservableCollection<MessageItem> Messages { get; } = new();
        public ObservableCollection<CallHistoryItem> CallHistory { get; } = new();

        public string CurrentUsername => _network.Session.Username;
        public string CurrentDisplayName => _network.Session.DisplayName;
        public string UserInitial => string.IsNullOrEmpty(CurrentDisplayName) ? "?" : CurrentDisplayName[0].ToString().ToUpper();

        public bool IsCallHistoryOpen
        {
            get => _isCallHistoryOpen;
            set { _isCallHistoryOpen = value; OnPropertyChanged(); }
        }

        public ContactItem? SelectedContact
        {
            get => _selectedContact;
            set
            {
                if (_selectedContact != null) _selectedContact.IsSelected = false;
                _selectedContact = value;
                if (_selectedContact != null)
                {
                    _selectedContact.IsSelected = true;
                    _selectedContact.UnreadCount = 0;
                    IsCallHistoryOpen = false;
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedContact));
                OnPropertyChanged(nameof(SelectedContactTitle));

                if (_selectedContact != null)
                {
                    _ = LoadConversationHistoryAsync(_selectedContact);
                }
            }
        }

        public bool HasSelectedContact => _selectedContact != null;
        public string SelectedContactTitle => _selectedContact?.DisplayName ?? "اختر جهة اتصال لبدء المحادثة";

        public string SearchQuery
        {
            get => _searchQuery;
            set { _searchQuery = value; OnPropertyChanged(); }
        }

        public string SearchResultText
        {
            get => _searchResultText;
            set { _searchResultText = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSearchResult)); }
        }

        public bool HasSearchResult => !string.IsNullOrEmpty(_searchResultText);

        public bool IsSearching
        {
            get => _isSearching;
            set { _isSearching = value; OnPropertyChanged(); }
        }

        public string InputMessage
        {
            get => _inputMessage;
            set
            {
                _inputMessage = value;
                OnPropertyChanged();
                NotifyTyping();
            }
        }

        public string SystemAnnouncement
        {
            get => _systemAnnouncement;
            set
            {
                _systemAnnouncement = value;
                _hasSystemAnnouncement = !string.IsNullOrEmpty(value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSystemAnnouncement));
            }
        }

        public bool HasSystemAnnouncement => _hasSystemAnnouncement;

        public ICommand SearchContactCommand { get; }
        public ICommand AddContactCommand { get; }
        public ICommand SendMessageCommand { get; }
        public ICommand AttachFileCommand { get; }
        public ICommand OpenFileCommand { get; }
        public ICommand SaveFileCommand { get; }
        public ICommand ToggleCallHistoryCommand { get; }
        public ICommand CallFromHistoryCommand { get; }
        public ICommand ClearChatHistoryCommand { get; }
        public ICommand DeleteContactCommand { get; }
        public ICommand StartAudioCallCommand { get; }
        public ICommand StartVideoCallCommand { get; }
        public ICommand DismissAnnouncementCommand { get; }
        public ICommand LogoutCommand { get; }

        public event Action<int, string, string, CallType>? CallInitiated;
        public event Action? LoggedOut;

        public ChatViewModel(ClientNetworkService network)
        {
            _network = network;
            _downloadsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SyncPulse_Downloads");
            if (!Directory.Exists(_downloadsDir)) Directory.CreateDirectory(_downloadsDir);

            SearchContactCommand = new RelayCommand(async () => await ExecuteSearchContactAsync());
            AddContactCommand = new RelayCommand(async () => await ExecuteAddContactAsync());
            SendMessageCommand = new RelayCommand(async () => await ExecuteSendMessageAsync());
            AttachFileCommand = new RelayCommand(async () => await ExecuteAttachFileAsync());
            OpenFileCommand = new RelayCommand<MessageItem>(ExecuteOpenFile);
            SaveFileCommand = new RelayCommand<MessageItem>(ExecuteSaveFile);
            ToggleCallHistoryCommand = new RelayCommand(async () => await ToggleCallHistoryAsync());
            CallFromHistoryCommand = new RelayCommand<CallHistoryItem>(ExecuteCallFromHistory);
            ClearChatHistoryCommand = new RelayCommand(async () => await ExecuteClearChatHistoryAsync());
            DeleteContactCommand = new RelayCommand<ContactItem>(ExecuteDeleteContact);
            StartAudioCallCommand = new RelayCommand(() => InitiateCall(CallType.Audio));
            StartVideoCallCommand = new RelayCommand(() => InitiateCall(CallType.Video));
            DismissAnnouncementCommand = new RelayCommand(() => SystemAnnouncement = string.Empty);
            LogoutCommand = new RelayCommand(() =>
            {
                _network.Disconnect();
                _network.Session.Clear();
                LoggedOut?.Invoke();
            });

            // تسجيل معالجات أحداث الشبكة
            _network.MessageReceived += OnMessageReceived;
            _network.MessageAckReceived += OnMessageAckReceived;
            _network.SystemBroadcastReceived += OnSystemBroadcastReceived;
            _network.PresenceChanged += OnPresenceChanged;
            _network.TypingIndicatorReceived += OnTypingIndicatorReceived;

            // تحميل جهات الاتصال الأولية والرسائل المعلقة
            _ = InitialSyncAsync();
        }

        public async Task InitialSyncAsync()
        {
            try
            {
                var req = new GetContactsListRequest { UserID = _network.Session.UserID };
                var res = await _network.SendRequestAsync<GetContactsListRequest, GetContactsListResponse>(
                    PacketType.GetContactsListRequest, req);

                if (res != null)
                {
                    App.Current?.Dispatcher.Invoke(() =>
                    {
                        Contacts.Clear();
                        foreach (var c in res.Contacts)
                        {
                            Contacts.Add(new ContactItem
                            {
                                ContactUserID = c.UserID,
                                Username = c.Username,
                                DisplayName = c.DisplayName,
                                IsOnline = c.IsOnline,
                                LastMessage = c.LastMessageContent ?? "لا توجد رسائل سابقة",
                                LastMessageTime = c.LastMessageTimestamp ?? DateTime.MinValue,
                                UnreadCount = c.UnreadCount
                            });
                        }

                        if (Contacts.Count > 0 && SelectedContact == null)
                        {
                            SelectedContact = Contacts[0];
                        }
                    });
                }
            }
            catch { }
        }

        private async Task ToggleCallHistoryAsync()
        {
            IsCallHistoryOpen = !IsCallHistoryOpen;
            if (IsCallHistoryOpen)
            {
                await LoadCallHistoryAsync();
            }
        }

        public async Task LoadCallHistoryAsync()
        {
            try
            {
                var req = new GetCallHistoryRequest { UserID = _network.Session.UserID };
                var res = await _network.SendRequestAsync<GetCallHistoryRequest, GetCallHistoryResponse>(
                    PacketType.GetCallHistoryRequest, req);

                if (res != null)
                {
                    App.Current?.Dispatcher.Invoke(() =>
                    {
                        CallHistory.Clear();
                        foreach (var call in res.Calls)
                        {
                            CallHistory.Add(call);
                        }
                    });
                }
            }
            catch { }
        }

        private void ExecuteCallFromHistory(CallHistoryItem? item)
        {
            if (item == null) return;
            int targetId = item.IsOutgoing ? item.ReceiverID : item.CallerID;
            string targetName = item.TargetPartyName;
            CallInitiated?.Invoke(targetId, targetName, targetName, item.CallType);
        }

        private void NotifyTyping()
        {
            if (SelectedContact == null || string.IsNullOrWhiteSpace(InputMessage)) return;

            if ((DateTime.UtcNow - _lastTypingSentTime).TotalSeconds > 2.5)
            {
                _lastTypingSentTime = DateTime.UtcNow;
                var packet = new TypingIndicatorPacket
                {
                    SenderID = _network.Session.UserID,
                    SenderUsername = _network.Session.Username,
                    ReceiverID = SelectedContact.ContactUserID,
                    IsTyping = true
                };
                _ = _network.SendPacketAsync(SyncPacket.Create(PacketType.TypingIndicator, packet));
            }
        }

        private void OnTypingIndicatorReceived(TypingIndicatorPacket typing)
        {
            App.Current?.Dispatcher.Invoke(() =>
            {
                var contact = Contacts.FirstOrDefault(c => c.ContactUserID == typing.SenderID);
                if (contact != null)
                {
                    contact.IsTyping = true;

                    if (_typingTimers.TryGetValue(contact.ContactUserID, out var existingTimer))
                    {
                        existingTimer.Stop();
                    }

                    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                    timer.Tick += (s, e) =>
                    {
                        contact.IsTyping = false;
                        timer.Stop();
                        _typingTimers.TryRemove(contact.ContactUserID, out _);
                    };
                    timer.Start();
                    _typingTimers[contact.ContactUserID] = timer;
                }
            });
        }

        private async Task ExecuteAttachFileAsync()
        {
            if (SelectedContact == null) return;

            var dlg = new OpenFileDialog
            {
                Title = "اختر ملفاً أو صورة لنقلها وحفظها",
                Filter = "كافة الملفات (*.*)|*.*|الصور (*.png;*.jpg;*.jpeg;*.gif)|*.png;*.jpg;*.jpeg;*.gif|المستندات (*.pdf;*.docx;*.txt)|*.pdf;*.docx;*.txt"
            };

            if (dlg.ShowDialog() == true)
            {
                string filePath = dlg.FileName;
                string fileName = Path.GetFileName(filePath);
                byte[] fileBytes = await File.ReadAllBytesAsync(filePath);

                var outgoingPacket = new ChatMessagePacket
                {
                    SenderID = _network.Session.UserID,
                    SenderUsername = _network.Session.Username,
                    ReceiverID = SelectedContact.ContactUserID,
                    Content = $"📁 ملف: {fileName}",
                    AttachmentPath = filePath,
                    AttachmentFileName = fileName,
                    AttachmentData = fileBytes,
                    AttachmentSize = fileBytes.Length,
                    Status = MessageStatus.Sent,
                    Timestamp = DateTime.UtcNow
                };

                var localItem = new MessageItem
                {
                    SenderID = _network.Session.UserID,
                    SenderUsername = _network.Session.Username,
                    ReceiverID = SelectedContact.ContactUserID,
                    Content = $"📁 ملف: {fileName}",
                    AttachmentPath = filePath,
                    AttachmentFileName = fileName,
                    AttachmentData = fileBytes,
                    AttachmentSize = fileBytes.Length,
                    Timestamp = DateTime.UtcNow,
                    Status = MessageStatus.Sent,
                    IsOutgoing = true
                };

                App.Current?.Dispatcher.Invoke(() =>
                {
                    Messages.Add(localItem);
                    SelectedContact.LastMessage = $"📁 ملف: {fileName}";
                    SelectedContact.LastMessageTime = DateTime.UtcNow;
                });

                await _network.SendPacketAsync(SyncPacket.Create(PacketType.DirectChatMessage, outgoingPacket));
            }
        }

        private void ExecuteOpenFile(MessageItem? item)
        {
            if (item == null) return;

            string? targetPath = item.AttachmentPath;

            // إذا لم يكن الملف موجوداً محلياً ولكن لدينا البايتات، نحفظه في مجلد التنزيلات
            if ((string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath)) && item.AttachmentData != null && item.AttachmentData.Length > 0)
            {
                string savePath = Path.Combine(_downloadsDir, item.DisplayFileName);
                File.WriteAllBytes(savePath, item.AttachmentData);
                item.AttachmentPath = savePath;
                targetPath = savePath;
            }

            if (!string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(targetPath) { UseShellExecute = true });
                }
                catch { }
            }
        }

        private void ExecuteSaveFile(MessageItem? item)
        {
            if (item == null) return;

            byte[]? data = item.AttachmentData;
            if (data == null && !string.IsNullOrEmpty(item.AttachmentPath) && File.Exists(item.AttachmentPath))
            {
                try { data = File.ReadAllBytes(item.AttachmentPath); } catch { }
            }

            if (data == null || data.Length == 0) return;

            var sfd = new SaveFileDialog
            {
                Title = "حفظ الملف المرفق",
                FileName = item.DisplayFileName,
                Filter = "الملف الأصلي|*" + Path.GetExtension(item.DisplayFileName) + "|كافة الملفات (*.*)|*.*"
            };

            if (sfd.ShowDialog() == true)
            {
                File.WriteAllBytes(sfd.FileName, data);
            }
        }

        private async Task ExecuteClearChatHistoryAsync()
        {
            if (SelectedContact == null) return;

            var clearPacket = new ClearChatHistoryPacket
            {
                UserID = _network.Session.UserID,
                TargetUserID = SelectedContact.ContactUserID
            };

            await _network.SendPacketAsync(SyncPacket.Create(PacketType.ClearChatHistoryRequest, clearPacket));

            App.Current?.Dispatcher.Invoke(() =>
            {
                Messages.Clear();
                if (SelectedContact != null)
                {
                    SelectedContact.LastMessage = "تم مسح سجل المحادثة";
                }
            });
        }

        private void ExecuteDeleteContact(ContactItem? contact)
        {
            if (contact == null) return;

            App.Current?.Dispatcher.Invoke(() =>
            {
                Contacts.Remove(contact);
                if (SelectedContact == contact)
                {
                    SelectedContact = Contacts.FirstOrDefault();
                }
            });
        }

        private async Task ExecuteSearchContactAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery)) return;

            IsSearching = true;
            SearchResultText = string.Empty;

            try
            {
                string cleanQuery = SearchQuery.Trim().TrimStart('@');
                var req = new SearchUserRequest { QueryUsername = cleanQuery };

                var res = await _network.SendRequestAsync<SearchUserRequest, SearchUserResponse>(
                    PacketType.SearchUserRequest, req);

                if (res != null && res.Found && res.User != null)
                {
                    SearchResultText = $"تم العثور على: {res.User.DisplayName} (@{res.User.Username})";
                }
                else
                {
                    SearchResultText = "لم يتم العثور على مستخدم بهذا الاسم.";
                }
            }
            catch
            {
                SearchResultText = "خطأ أثناء البحث.";
            }
            finally
            {
                IsSearching = false;
            }
        }

        private async Task ExecuteAddContactAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery)) return;

            string cleanQuery = SearchQuery.Trim().TrimStart('@');
            var searchReq = new SearchUserRequest { QueryUsername = cleanQuery };
            var searchRes = await _network.SendRequestAsync<SearchUserRequest, SearchUserResponse>(
                PacketType.SearchUserRequest, searchReq);

            if (searchRes == null || !searchRes.Found || searchRes.User == null)
            {
                SearchResultText = "المستخدم غير موجود لإضافته.";
                return;
            }

            var addReq = new AddContactRequest
            {
                OwnerUserID = _network.Session.UserID,
                ContactUserID = searchRes.User.UserID,
                CustomName = searchRes.User.DisplayName
            };

            var addRes = await _network.SendRequestAsync<AddContactRequest, AddContactResponse>(
                PacketType.AddContactRequest, addReq);

            if (addRes != null && addRes.Success)
            {
                SearchResultText = $"✓ تمت إضافة {searchRes.User.DisplayName} بنجاح!";
                SearchQuery = string.Empty;

                var existing = Contacts.FirstOrDefault(c => c.ContactUserID == searchRes.User.UserID);
                if (existing == null)
                {
                    var newContact = new ContactItem
                    {
                        ContactUserID = searchRes.User.UserID,
                        Username = searchRes.User.Username,
                        DisplayName = searchRes.User.DisplayName,
                        IsOnline = searchRes.User.IsOnline,
                        LastMessage = "جهة اتصال جديدة"
                    };
                    Contacts.Insert(0, newContact);
                    SelectedContact = newContact;
                }
            }
            else
            {
                SearchResultText = addRes?.Message ?? "تعذر إضافة جهة الاتصال.";
            }
        }

        private async Task LoadConversationHistoryAsync(ContactItem contact)
        {
            try
            {
                var historyReq = new SyncHistoryRequestPacket
                {
                    UserID = _network.Session.UserID,
                    LastKnownMessageID = 0
                };

                var res = await _network.SendRequestAsync<SyncHistoryRequestPacket, SyncHistoryResponsePacket>(
                    PacketType.SyncHistoryRequest, historyReq);

                if (res != null)
                {
                    var chatMessages = res.Messages
                        .Where(m => (m.SenderID == contact.ContactUserID && m.ReceiverID == _network.Session.UserID) ||
                                    (m.SenderID == _network.Session.UserID && m.ReceiverID == contact.ContactUserID))
                        .OrderBy(m => m.Timestamp)
                        .ToList();

                    App.Current?.Dispatcher.Invoke(() =>
                    {
                        Messages.Clear();
                        foreach (var msg in chatMessages)
                        {
                            Messages.Add(new MessageItem
                            {
                                MessageID = msg.MessageID,
                                ConversationID = msg.ConversationID,
                                SenderID = msg.SenderID,
                                SenderUsername = msg.SenderUsername,
                                ReceiverID = msg.ReceiverID,
                                Content = msg.Content,
                                AttachmentPath = msg.AttachmentPath,
                                AttachmentFileName = msg.AttachmentFileName,
                                AttachmentData = msg.AttachmentData,
                                AttachmentSize = msg.AttachmentSize,
                                Timestamp = msg.Timestamp,
                                Status = msg.Status,
                                IsOutgoing = msg.SenderID == _network.Session.UserID
                            });

                            if (msg.ReceiverID == _network.Session.UserID && msg.Status != MessageStatus.Read)
                            {
                                var readAck = new MessageAckPacket
                                {
                                    MessageID = msg.MessageID,
                                    ConversationID = msg.ConversationID,
                                    SenderID = _network.Session.UserID,
                                    ReceiverID = msg.SenderID,
                                    NewStatus = MessageStatus.Read,
                                    AcknowledgedAt = DateTime.UtcNow
                                };
                                _ = _network.SendPacketAsync(SyncPacket.Create(PacketType.MessageReadAck, readAck));
                            }
                        }
                    });
                }
            }
            catch { }
        }

        private async Task ExecuteSendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(InputMessage) || SelectedContact == null) return;

            string contentToSend = InputMessage.Trim();
            InputMessage = string.Empty;

            var outgoingPacket = new ChatMessagePacket
            {
                SenderID = _network.Session.UserID,
                SenderUsername = _network.Session.Username,
                ReceiverID = SelectedContact.ContactUserID,
                Content = contentToSend,
                Status = MessageStatus.Sent,
                Timestamp = DateTime.UtcNow
            };

            var localItem = new MessageItem
            {
                SenderID = _network.Session.UserID,
                SenderUsername = _network.Session.Username,
                ReceiverID = SelectedContact.ContactUserID,
                Content = contentToSend,
                Timestamp = DateTime.UtcNow,
                Status = MessageStatus.Sent,
                IsOutgoing = true
            };

            App.Current?.Dispatcher.Invoke(() =>
            {
                Messages.Add(localItem);
                SelectedContact.LastMessage = contentToSend;
                SelectedContact.LastMessageTime = DateTime.UtcNow;
            });

            await _network.SendPacketAsync(SyncPacket.Create(PacketType.DirectChatMessage, outgoingPacket));
        }

        private void OnMessageReceived(ChatMessagePacket msg)
        {
            // حفظ المرفق تلقائياً في مجلد التنزيلات إن وجد
            if (msg.AttachmentData != null && msg.AttachmentData.Length > 0 && !string.IsNullOrEmpty(msg.AttachmentFileName))
            {
                try
                {
                    string localSavedPath = Path.Combine(_downloadsDir, msg.AttachmentFileName);
                    File.WriteAllBytes(localSavedPath, msg.AttachmentData);
                    msg.AttachmentPath = localSavedPath;
                }
                catch { }
            }

            App.Current?.Dispatcher.Invoke(() =>
            {
                var contact = Contacts.FirstOrDefault(c => c.ContactUserID == msg.SenderID);
                if (contact == null)
                {
                    contact = new ContactItem
                    {
                        ContactUserID = msg.SenderID,
                        Username = msg.SenderUsername,
                        DisplayName = msg.SenderUsername,
                        IsOnline = true
                    };
                    Contacts.Insert(0, contact);
                }

                contact.LastMessage = msg.Content;
                contact.LastMessageTime = msg.Timestamp;

                if (SelectedContact != null && SelectedContact.ContactUserID == msg.SenderID)
                {
                    Messages.Add(new MessageItem
                    {
                        MessageID = msg.MessageID,
                        ConversationID = msg.ConversationID,
                        SenderID = msg.SenderID,
                        SenderUsername = msg.SenderUsername,
                        ReceiverID = msg.ReceiverID,
                        Content = msg.Content,
                        AttachmentPath = msg.AttachmentPath,
                        AttachmentFileName = msg.AttachmentFileName,
                        AttachmentData = msg.AttachmentData,
                        AttachmentSize = msg.AttachmentSize,
                        Timestamp = msg.Timestamp,
                        Status = MessageStatus.Read,
                        IsOutgoing = false
                    });

                    var ack = new MessageAckPacket
                    {
                        MessageID = msg.MessageID,
                        ConversationID = msg.ConversationID,
                        SenderID = _network.Session.UserID,
                        ReceiverID = msg.SenderID,
                        NewStatus = MessageStatus.Read,
                        AcknowledgedAt = DateTime.UtcNow
                    };
                    _ = _network.SendPacketAsync(SyncPacket.Create(PacketType.MessageReadAck, ack));
                }
                else
                {
                    contact.UnreadCount++;
                }
            });
        }

        private void OnMessageAckReceived(MessageAckPacket ack)
        {
            App.Current?.Dispatcher.Invoke(() =>
            {
                var msg = Messages.FirstOrDefault(m => m.MessageID == ack.MessageID);
                if (msg == null && ack.MessageID > 0)
                {
                    msg = Messages.LastOrDefault(m => m.IsOutgoing && (m.MessageID == 0 || m.MessageID == ack.MessageID));
                    if (msg != null) msg.MessageID = ack.MessageID;
                }

                if (msg != null)
                {
                    msg.Status = ack.NewStatus;
                }
            });
        }

        private void OnSystemBroadcastReceived(string broadcastMessage)
        {
            App.Current?.Dispatcher.Invoke(() =>
            {
                SystemAnnouncement = $"📢 تنبيه عام: {broadcastMessage}";
            });
        }

        private void OnPresenceChanged(UserPresenceChangedPacket presence)
        {
            App.Current?.Dispatcher.Invoke(() =>
            {
                var contact = Contacts.FirstOrDefault(c => c.ContactUserID == presence.UserID);
                if (contact != null)
                {
                    contact.IsOnline = presence.IsOnline;
                }
            });
        }

        private void InitiateCall(CallType type)
        {
            if (SelectedContact == null) return;
            CallInitiated?.Invoke(SelectedContact.ContactUserID, SelectedContact.Username, SelectedContact.DisplayName, type);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
