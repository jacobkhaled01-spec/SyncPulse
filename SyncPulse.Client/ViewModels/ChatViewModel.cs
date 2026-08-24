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
        private DateTime _lastTypingSentTime = DateTime.MinValue;
        private readonly ConcurrentDictionary<int, DispatcherTimer> _typingTimers = new();

        public ObservableCollection<ContactItem> Contacts { get; } = new();
        public ObservableCollection<MessageItem> Messages { get; } = new();

        public string CurrentUsername => _network.Session.Username;
        public string CurrentDisplayName => _network.Session.DisplayName;
        public string UserInitial => string.IsNullOrEmpty(CurrentDisplayName) ? "?" : CurrentDisplayName[0].ToString().ToUpper();

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

            SearchContactCommand = new RelayCommand(async () => await ExecuteSearchContactAsync());
            AddContactCommand = new RelayCommand(async () => await ExecuteAddContactAsync());
            SendMessageCommand = new RelayCommand(async () => await ExecuteSendMessageAsync());
            AttachFileCommand = new RelayCommand(async () => await ExecuteAttachFileAsync());
            OpenFileCommand = new RelayCommand<string>(ExecuteOpenFile);
            ClearChatHistoryCommand = new RelayCommand(ExecuteClearChatHistory);
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

        private void NotifyTyping()
        {
            if (SelectedContact == null || string.IsNullOrWhiteSpace(InputMessage)) return;

            // كبح إرسال حزم الكتابة بمعدل مرة واحدة كل 2.5 ثانية
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

                    // إعادة تعيين مؤقت إخفاء شارة الكتابة بعد 3 ثوانٍ
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
                Title = "اختر ملفاً أو صورة لإرسالها",
                Filter = "كافة الملفات المدعومة (*.*)|*.*|الصور (*.png;*.jpg;*.jpeg;*.gif)|*.png;*.jpg;*.jpeg;*.gif|المستندات (*.pdf;*.docx;*.txt)|*.pdf;*.docx;*.txt"
            };

            if (dlg.ShowDialog() == true)
            {
                string filePath = dlg.FileName;
                string fileName = Path.GetFileName(filePath);

                var outgoingPacket = new ChatMessagePacket
                {
                    SenderID = _network.Session.UserID,
                    SenderUsername = _network.Session.Username,
                    ReceiverID = SelectedContact.ContactUserID,
                    Content = $"📁 مرفق: {fileName}",
                    AttachmentPath = filePath,
                    Status = MessageStatus.Sent,
                    Timestamp = DateTime.UtcNow
                };

                var localItem = new MessageItem
                {
                    SenderID = _network.Session.UserID,
                    SenderUsername = _network.Session.Username,
                    ReceiverID = SelectedContact.ContactUserID,
                    Content = $"📁 مرفق: {fileName}",
                    AttachmentPath = filePath,
                    Timestamp = DateTime.UtcNow,
                    Status = MessageStatus.Sent,
                    IsOutgoing = true
                };

                App.Current?.Dispatcher.Invoke(() =>
                {
                    Messages.Add(localItem);
                    SelectedContact.LastMessage = $"📁 مرفق: {fileName}";
                    SelectedContact.LastMessageTime = DateTime.UtcNow;
                });

                await _network.SendPacketAsync(SyncPacket.Create(PacketType.DirectChatMessage, outgoingPacket));
            }
        }

        private void ExecuteOpenFile(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

            try
            {
                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            }
            catch { }
        }

        private void ExecuteClearChatHistory()
        {
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
                                Timestamp = msg.Timestamp,
                                Status = msg.Status,
                                IsOutgoing = msg.SenderID == _network.Session.UserID
                            });

                            // إشعار قراءة الرسائل الواردة غير المقروءة
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
                        Timestamp = msg.Timestamp,
                        Status = MessageStatus.Read,
                        IsOutgoing = false
                    });

                    // إرسال تأكيد قراءة مباشر لأن المحادثة مفتوحة حالياً
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
