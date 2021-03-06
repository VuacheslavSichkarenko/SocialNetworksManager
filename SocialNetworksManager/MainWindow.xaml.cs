﻿using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Threading;

using SocialNetworksManager.Contracts;
using SocialNetworksManager.DataPresentation;

using MahApps.Metro.Controls;

using CefSharp;
using CefSharp.Wpf;

namespace SocialNetworksManager
{
    [Export(typeof(IApplicationContract))]
    public partial class MainWindow : MetroWindow, IApplicationContract
    {
        //MEF components
        private DirectoryCatalog directory_catalog = null;
        private CompositionContainer composition_container = null;
        private ImportManager import_manager = null;

        //Lists
        private List<SocialNetworksListItem> social_networks_list_items = new List<SocialNetworksListItem>();
        private List<FriendsListItem> friends_list_items = new List<FriendsListItem>();
        private List<PhotosListItem> photos_list_items = new List<PhotosListItem>();
        private List<GroupsListItem> groups_list_items = new List<GroupsListItem>();
        private List<SendMessageStatus> messages_statuses = new List<SendMessageStatus>();

        //Other fields
        private UserInfo photos_user_info = null;
        private Thread check_connection_thread;
        private SpecialWindow special_window;
        private Boolean IsContainerInitialized = false;

        //Delegates
        private delegate void SetNoConnectionPageVisibilityDelegate(Boolean isVisible);

        public MainWindow()
        {
            Closing += MainWindow_Closing;

            InitializeComponent();
            InitializeContainer();
            if (!IsContainerInitialized) pages.IsEnabled = false;
            else RefreshExtensions();
            InitializeThreads();
            InitializeCef();
        }

        #region UsualMethods
        private void SetNoConnectionPageVisibility(Boolean isVisible)
        {
            if (isVisible == true && noConnetionPage.Visibility != Visibility.Visible)
            {
                pages.Visibility = Visibility.Collapsed;
                noConnetionPage.Visibility = Visibility.Visible;
                button_refresh_extensions.IsEnabled = false;
            }
            else if (isVisible == false && noConnetionPage.Visibility != Visibility.Collapsed)
            {
                pages.Visibility = Visibility.Visible;
                noConnetionPage.Visibility = Visibility.Collapsed;
                button_refresh_extensions.IsEnabled = true;
            }
        }

        private void CheckConnectionThreadProc()
        {
            int description;
            SetNoConnectionPageVisibilityDelegate @delegate = new SetNoConnectionPageVisibilityDelegate(SetNoConnectionPageVisibility);

            while (true)
            {
                if (Helpers.NetHelper.InternetGetConnectedState(out description, 0)) Dispatcher.Invoke(@delegate, false);
                else Dispatcher.Invoke(@delegate, true);
            }
        }

        private void InitializeCef()
        {
            CefSettings settings = new CefSettings();
            settings.CachePath = Environment.CurrentDirectory + "/BrowserCache";
            Cef.Initialize(settings);
        }

        private void InitializeThreads()
        {
            check_connection_thread = new Thread(CheckConnectionThreadProc);
            check_connection_thread.IsBackground = true;

            check_connection_thread.Start();
        }

        private void InitializeContainer()
        {
            String dirPath = Environment.CurrentDirectory + "\\bin";

            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
                MessageBox.Show("Extensions directory does not exists.\nExtensions did not loaded.");
                IsContainerInitialized = false;
                return;
            }
            if (Directory.GetFiles(dirPath).Length == 0)
            {
                MessageBox.Show("There are no extensions.");
                IsContainerInitialized = false;
                return;
            }

            directory_catalog = new DirectoryCatalog(dirPath);
            composition_container = new CompositionContainer(directory_catalog);
            import_manager = new ImportManager();

            try
            {
                composition_container.ComposeParts(this, import_manager);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error");
                Environment.Exit(1);
            }

            IsContainerInitialized = true;
        }

        private void RefreshExtensions()
        {
            if (!IsContainerInitialized)
            {
                InitializeContainer();
                if (!IsContainerInitialized) return;
                else pages.IsEnabled = true;
            }

            if (directory_catalog == null) return;
            directory_catalog.Refresh();
            socialNetworksHolder.ItemsSource = null;

            social_networks_list_items = new List<SocialNetworksListItem>();

            foreach (Lazy<ISocialNetworksManagerExtension> extension in import_manager.extensionsCollection)
            {
                SocialNetworksListItem methodsItem = new SocialNetworksListItem();
                methodsItem.Name = extension.Value.getSocialNetworkName();
                methodsItem.AuthorizedUsers = extension.Value.getAuthorizedUsers();

                social_networks_list_items.Add(methodsItem);
            }

            socialNetworksHolder.ItemsSource = social_networks_list_items;
        }

        private ISocialNetworksManagerExtension findSocialNetworkExtensionByName(String name)
        {
            foreach (Lazy<ISocialNetworksManagerExtension> extension in import_manager.extensionsCollection)
            {
                if (extension.Value.getSocialNetworkName().Equals(name))
                {
                    return extension.Value;
                }
            }

            return null;
        }

        private void UpdateFriends()
        {
            friendsList.ItemsSource = null;
            friends_list_items.Clear();

            foreach (Lazy<ISocialNetworksManagerExtension> item in import_manager.extensionsCollection)
            {
                item.Value.GetFriends();
            }

            social_network_combobox.Items.Clear();
            user_name_combobox.Items.Clear();
            social_network_combobox.Items.Add(new ComboBoxItem() { Content = "None" });
            user_name_combobox.Items.Add(new ComboBoxItem() { Content = "None" });
            social_network_combobox.SelectedItem = social_network_combobox.Items[0];
            user_name_combobox.SelectedItem = user_name_combobox.Items[0];

            Boolean has_this_item = false;

            foreach (FriendsListItem friend_list_item in friends_list_items)
            {
                for (int i = 0; i < social_network_combobox.Items.Count; i++)
                {
                    ComboBoxItem combo_box_item = social_network_combobox.Items[i] as ComboBoxItem;

                    if((String)combo_box_item.Content == friend_list_item.SocialNetworkName)
                    {
                        has_this_item = true;
                        break;
                    }
                }

                if(!has_this_item)
                {
                    social_network_combobox.Items.Add(new ComboBoxItem() { Content = friend_list_item.SocialNetworkName});
                }

                has_this_item = false;
            }

            friendsList.ItemsSource = friends_list_items;
        }

        private void UpdatePhotos()
        {
            myphotos_socialnetworks_buttons.Items.Clear();
            friendsphotos_socialnetworks_buttons.Items.Clear();

            foreach (Lazy<ISocialNetworksManagerExtension> item in import_manager.extensionsCollection)
            {
                String socNetName = item.Value.getSocialNetworkName();
                List<UserInfo> socNetUsers = item.Value.getAuthorizedUsers();

                TreeViewItem treeViewItem = new TreeViewItem();
                treeViewItem.Header = socNetName;

                foreach (UserInfo userInfo in socNetUsers)
                {
                    ButtonWithUserInfo buttonWithUserInfo = new ButtonWithUserInfo();
                    buttonWithUserInfo.User = userInfo;
                    buttonWithUserInfo.Content = userInfo.Name;
                    buttonWithUserInfo.Click += ButtonWithUserInfo_Click;

                    treeViewItem.Items.Add(buttonWithUserInfo);
                }

                myphotos_socialnetworks_buttons.Items.Add(treeViewItem);
            }

            foreach (Lazy<ISocialNetworksManagerExtension> item in import_manager.extensionsCollection)
            {
                String socNetName = item.Value.getSocialNetworkName();

                friends_list_items.Clear();
                item.Value.GetFriends();

                TreeViewItem treeViewItem = new TreeViewItem();
                treeViewItem.Header = socNetName;

                foreach (FriendsListItem listItem in friends_list_items)
                {
                    ButtonWithUserInfo buttonWithUserInfo = new ButtonWithUserInfo();
                    buttonWithUserInfo.User = listItem.Friend;
                    buttonWithUserInfo.Content = listItem.Friend.Name;
                    buttonWithUserInfo.User.SocialNetworkName = item.Value.getSocialNetworkName();
                    buttonWithUserInfo.Click += ButtonWithUserInfo_Click;

                    treeViewItem.Items.Add(buttonWithUserInfo);
                }

                friendsphotos_socialnetworks_buttons.Items.Add(treeViewItem);
            }
        }

        private void UpdateGroups()
        {
            groups_holder.Items.Clear();

            foreach (Lazy<ISocialNetworksManagerExtension> item in import_manager.extensionsCollection)
            {
                groups_list_items.Clear();
                item.Value.GetGroups();

                String soc_net_name = item.Value.getSocialNetworkName();
                String user_name = "";

                TreeViewItem soc_net_treeviewitem = new TreeViewItem();
                soc_net_treeviewitem.Header = soc_net_name;

                foreach (GroupsListItem user_item in groups_list_items)
                {
                    if(user_name != user_item.User.Name)
                    {
                        TreeViewItem user_treeviewitem = new TreeViewItem();
                        user_treeviewitem.Header = user_item.User.Name;
                        user_name = user_item.User.Name;

                        foreach (GroupsListItem group_item in groups_list_items)
                        {
                            if (group_item.User.ID == user_item.User.ID)
                            {
                                TreeViewItem group_treeviewitem = new TreeViewItem();
                                group_treeviewitem.Header = group_item.GroupName;

                                user_treeviewitem.Items.Add(group_treeviewitem);
                            }
                        }

                        soc_net_treeviewitem.Items.Add(user_treeviewitem);
                    }
                }

                groups_holder.Items.Add(soc_net_treeviewitem);
            }
        }
        #endregion

        #region ContractMethods
        public void AddItemsToFriendsList(List<FriendsListItem> items)
        {
            friends_list_items.AddRange(items);
        }

        public void AddItemsToPhotosList(List<PhotosListItem> items)
        {
            photos_list_items.AddRange(items);
        }

        public void AddItemsToGroupsList(List<GroupsListItem> items)
        {
            groups_list_items.AddRange(items);
        }

        public void OpenSpecialWindow(Uri uri, Uri redirect_uri, Dictionary<String, String> parameters)
        {
            special_window = new SpecialWindow(uri, redirect_uri, parameters);
            try
            {
                special_window.ShowDialog();
            }
            catch (Exception ex)
            {
                OpenSpecialWindow(ex.Message);
            }
        }

        public void OpenSpecialWindow(UserControl userControl)
        {
            special_window = new SpecialWindow(userControl);
            special_window.ShowDialog();
        }

        public void OpenSpecialWindow(String text)
        {
            special_window = new SpecialWindow(text);
            special_window.ShowDialog();
        }

        public void CloseSpecialWindow()
        {
            special_window.Close();
        }

        public List<FriendsListItem> GetFriendsListItems()
        {
            return friends_list_items;
        }

        public string GetMessage()
        {
            return message_text_box.Text;
        }

        public void AddSendMessageStatuses(List<SendMessageStatus> statuses)
        {
            messages_statuses.AddRange(statuses);
        }

        public void SetPhotosListSatusData(String data)
        {
            photoslist_satus_data.Content = data;
        }

        public string GetPhotoUserID()
        {
            return photos_user_info.ID;
        }

        public ulong GetPhotosCount()
        {
            return (ulong)photos_list_items.Count;
        }

        public void DisableNextPhotosButton()
        {
            next_photos_button.IsEnabled = false;
        }
        #endregion

        #region EventMethods
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Cef.Shutdown();
        }

        private void Button_RefreshPhotos_Click(object sender, RoutedEventArgs e)
        {
            if (photos_user_info == null) return;

            next_photos_button.IsEnabled = true;

            photos_list_items.Clear();
            photos_holder.Children.Clear();

            findSocialNetworkExtensionByName(photos_user_info.SocialNetworkName).GetPhotos();

            for (int i = 0; i < photos_list_items.Count; i++)
            {
                Image photo = photos_list_items[i].Photo;
                photo.Style = FindResource("PhotoStyle") as Style;

                Border photoBorder = new Border();
                photoBorder.Style = FindResource("PhotoBorderStyle") as Style;
                photoBorder.Child = photo;

                photos_holder.Children.Add(photoBorder);
            }
        }

        private void Button_Next_Click(object sender, RoutedEventArgs e)
        {
            if (photos_user_info == null) return;

            photo_size_slider.Value = photo_size_slider.Minimum;

            int from_pos = photos_list_items.Count;

            findSocialNetworkExtensionByName(photos_user_info.SocialNetworkName).GetPhotos();

            for (int i = from_pos; i < photos_list_items.Count; i++)
            {
                Image photo = photos_list_items[i].Photo;
                photo.Style = FindResource("PhotoStyle") as Style;

                Border photoBorder = new Border();
                photoBorder.Style = FindResource("PhotoBorderStyle") as Style;
                photoBorder.Child = photo;

                photos_holder.Children.Add(photoBorder);
            }
        }

        private void ButtonWithUserInfo_Click(object sender, RoutedEventArgs e)
        {
            ButtonWithUserInfo buttonWithUserInfo = sender as ButtonWithUserInfo;

            if (buttonWithUserInfo.User == null) return;

            next_photos_button.IsEnabled = true;

            photos_user_info = new UserInfo();
            photos_user_info.ID = buttonWithUserInfo.User.ID;
            photos_user_info.Name = buttonWithUserInfo.User.Name;
            photos_user_info.SocialNetworkName = buttonWithUserInfo.User.SocialNetworkName;
            username_textbox.Text = buttonWithUserInfo.User.Name;
            photo_size_slider.Value = photo_size_slider.Minimum;

            photos_list_items.Clear();
            photos_holder.Children.Clear();

            findSocialNetworkExtensionByName(buttonWithUserInfo.User.SocialNetworkName).GetPhotos();

            for (int i = 0; i < photos_list_items.Count; i++)
            {
                Image photo = photos_list_items[i].Photo;
                photo.Style = FindResource("PhotoStyle") as Style;

                Border photoBorder = new Border();
                photoBorder.Style = FindResource("PhotoBorderStyle") as Style;
                photoBorder.Child = photo;

                photos_holder.Children.Add(photoBorder);
            }
        }

        private void Button_RefreshExtensions_Click(object sender, RoutedEventArgs e)
        {
            RefreshExtensions();
        }

        private void Button_SendMessage_Click(object sender, RoutedEventArgs e)
        {
            messages_statuses.Clear();

            foreach (Lazy<ISocialNetworksManagerExtension> item in import_manager.extensionsCollection)
            {
                item.Value.SendMessageToSelectedFriends();
            }

            if (messages_statuses.Count == 0) return;

            StringBuilder messagesStatusesString = new StringBuilder();

            foreach (SendMessageStatus status in messages_statuses)
            {
                messagesStatusesString.AppendFormat("{0}: Message from {1} to {2} {3}.\n", status.SocialNetworkName, status.UserNameFrom, status.UserNameTo, status.IsMessageSended == true ? "Sended" : "Not Sended");
            }

            OpenSpecialWindow(messagesStatusesString.ToString());
        }

        private void Button_Auth_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            SocialNetworksListItem item = (button).DataContext as SocialNetworksListItem;
            ISocialNetworksManagerExtension extension = findSocialNetworkExtensionByName(item.Name);
            extension.Authorization();

            item.AuthorizedUsers = extension.getAuthorizedUsers();
        }

        private void Button_SelectAllFriends_Click(object sender, RoutedEventArgs e)
        {
            foreach (FriendsListItem item in friends_list_items)
            {
                item.IsChecked = true;
            }
        }

        private void Button_DeselectAllFriends_Click(object sender, RoutedEventArgs e)
        {
            foreach (FriendsListItem item in friends_list_items)
            {
                item.IsChecked = false;
            }
        }

        private void Button_SelectWithFilter_Click(object sender, RoutedEventArgs e)
        {
            foreach (FriendsListItem item in friends_list_items)
            {
                ComboBoxItem soc_net = social_network_combobox.SelectedItem as ComboBoxItem;
                ComboBoxItem user_name = user_name_combobox.SelectedItem as ComboBoxItem;

                if ((String)soc_net.Content == "None" && (String)user_name.Content != "None")
                {
                    if ((String)user_name.Content == item.User.Name)
                    {
                        item.IsChecked = true;
                    }
                    else item.IsChecked = false;
                }
                else if ((String)soc_net.Content != "None" && (String)user_name.Content == "None")
                {
                    if ((String)soc_net.Content == item.SocialNetworkName)
                    {
                        item.IsChecked = true;
                    }
                    else item.IsChecked = false;
                }
                else if ((String)user_name.Content != "None" && (String)soc_net.Content != "None")
                {
                    if ((String)user_name.Content == item.User.Name && (String)soc_net.Content == item.SocialNetworkName)
                    {
                        item.IsChecked = true;
                    }
                    else item.IsChecked = false;
                }
                else item.IsChecked = false;
            }
        }

        private void pages_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is MetroAnimatedTabControl)
            {
                MetroAnimatedTabControl tabControl = sender as MetroAnimatedTabControl;
                MetroTabItem tabItem = tabControl.SelectedItem as MetroTabItem;

                switch (tabItem.Header)
                {
                    case "Friends":
                        UpdateFriends();
                        break;
                    case "Photos":
                        UpdatePhotos();
                        break;
                    case "Groups":
                        UpdateGroups();
                        break;
                    default:
                        break;
                }
            }
        }

        private void Slider_PhotoSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UIElementCollection element_collection = photos_holder?.Children;

            if (element_collection == null) return;

            foreach (UIElement item in element_collection)
            {
                Image img = (item as Border).Child as Image;

                img.Width = e.NewValue;
                img.Height = e.NewValue;
            }
        }

        private void social_network_combobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (social_network_combobox.SelectedItem == null) return;

            Boolean has_this_item = false;

            user_name_combobox.Items.Clear();
            user_name_combobox.Items.Add(new ComboBoxItem() { Content = "None" });
            user_name_combobox.SelectedItem = user_name_combobox.Items[0];

            foreach (FriendsListItem friend_list_item in friends_list_items)
            {
                ComboBoxItem selected_soc_net = social_network_combobox.SelectedItem as ComboBoxItem;

                if ((String)selected_soc_net.Content == friend_list_item.SocialNetworkName)
                {
                    for (int i = 0; i < user_name_combobox.Items.Count; i++)
                    {
                        ComboBoxItem combo_box_item = user_name_combobox.Items[i] as ComboBoxItem;

                        if ((String)combo_box_item.Content == friend_list_item.User.Name)
                        {
                            has_this_item = true;
                            break;
                        }
                    }

                    if (!has_this_item)
                    {
                        user_name_combobox.Items.Add(new ComboBoxItem() { Content = friend_list_item.User.Name });
                    }

                    has_this_item = false;
                }
            }
        }
        #endregion
    }
}