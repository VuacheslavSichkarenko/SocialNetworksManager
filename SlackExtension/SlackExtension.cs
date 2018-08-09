﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;
using System.Windows;

using SocialNetworksManager.Contracts;
using SocialNetworksManager.DataPresentation;

using System.Threading;

namespace SlackExtension
{
    [Export(typeof(ISocialNetworksManagerExtension))]
    public class SlackExtension : ISocialNetworksManagerExtension
    {
        [Import(typeof(IApplicationContract),AllowRecomposition = true)]
        public IApplicationContract applicationContract;

        private SlackHelper slackHelper = null;

        public SlackExtension()
        {
            
        }

        public string getSocialNetworkName()
        {
            return "Slack";
        }

        public string getExtensionName()
        {
            return "Slack_Extension";
        }

        public bool GetAuthStatus()
        {
            if (slackHelper == null) return false;
            else return slackHelper.IsAuthorized;
        }

        public void Authorization()
        {
            if (GetAuthStatus()) return;

            slackHelper = new SlackHelper(applicationContract);
            slackHelper.Authorize();      
        }

        public void GetFriends()
        {
            if (!GetAuthStatus()) return;

            List<Models.SlackUser> users = slackHelper.GetUsers();

            List<FriendsListItem> friendsItems = new List<FriendsListItem>();

            foreach (Models.SlackUser user in users)
            {
                FriendsListItem friendItem = new FriendsListItem();
                friendItem.SocialNetworkName = getSocialNetworkName();
                friendItem.FriendName = user.Profile.RealName;
                friendItem.ID = user.ID;
                friendsItems.Add(friendItem);
            }

            applicationContract.AddItemsToFriendsList(friendsItems);
        }

        public void GetPhotos()
        {
            if (!GetAuthStatus()) return;

            List<Models.SlackFile> files = slackHelper.GetPhotos();

            List<PhotosListItem> photosItems = new List<PhotosListItem>();

            foreach (Models.SlackFile file in files)
            {
                PhotosListItem photoItem = new PhotosListItem();
                photoItem.SocialNetworkName = getSocialNetworkName();
                photoItem.PhotoSource = file.Thumb80;

                photosItems.Add(photoItem);
            }

            applicationContract.AddItemsToPhotosList(photosItems);
        }

        public void SendMessageToSelectedFriends()
        {
            if (!GetAuthStatus()) return;

            List<FriendsListItem> items = applicationContract.GetFriendsListItems();
            List<Models.SlackIM> ims = slackHelper.GetIms();

            foreach (FriendsListItem item in items)
            {
                if (!item.SocialNetworkName.Equals(getSocialNetworkName())) continue;
                if (!item.IsChecked) continue;

                String channel_id = null;

                foreach (Models.SlackIM im in ims)
                {
                    if(item.ID == im.User)
                    {
                        channel_id = im.ID;
                    }
                }
                
                slackHelper.SendMessage(channel_id, applicationContract.GetMessage());
            }
        }
    }
}