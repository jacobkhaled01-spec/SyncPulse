using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
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
                if (_selectedContact != null) _selectedContact.IsSelected = true;

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
            set { _inputMessage = value; OnPropertyChanged(); }
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
                                LastMessage = c.LastMessageContent ?? "لا توجد رسائل",
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
                        IsOnline = searchRes.User.IsOnline
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
            Messages.Clear();

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
                        .OrderBy(m => m.Timestamp);

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
                    }
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

            Messages.Add(localItem);

            SelectedContact.LastMessage = contentToSend;
            SelectedContact.LastMessageTime = DateTime.UtcNow;

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
                        Status = MessageStatus.Delivered,
                        IsOutgoing = false
                    });

                    var ack = new MessageAckPacket
                    {
                        MessageID = msg.MessageID,
                        ConversationID = msg.ConversationID,
                        SenderID = _network.Session.UserID,
                        ReceiverID = msg.SenderID,
                        NewStatus = MessageStatus.Delivered,
                        AcknowledgedAt = DateTime.UtcNow
                    };
                    _ = _network.SendPacketAsync(SyncPacket.Create(PacketType.MessageDeliveryAck, ack));
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
