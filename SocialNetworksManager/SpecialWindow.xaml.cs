﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Text.RegularExpressions;
using System.Diagnostics;

using Helpers;

using DotNetBrowser;
using DotNetBrowser.WPF;

using MahApps.Metro.Controls;

namespace SocialNetworksManager
{
    public partial class SpecialWindow : Window
    {
        private BrowserView webBrowser;
        private Regex checkRegex;
        private Dictionary<String, String> parameters;

        private delegate void CloseWindowDelegate();

        public SpecialWindow(Uri uri, Uri redirect_uri, Dictionary<String, String> parameters)
        {
            InitializeComponent();

            checkRegex = new Regex("^" + redirect_uri.ToString());
            this.parameters = parameters;
            Closing += SpecialWindow_Closing;

            webBrowser = new WPFBrowserView();
            webBrowser.Browser.DocumentLoadedInFrameEvent += Browser_DocumentLoadedInFrameEvent;
            controlHolder.Children.Add((UIElement)webBrowser.GetComponent());
            webBrowser.Browser.LoadURL(uri.ToString());
        }

        private void SpecialWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            webBrowser.Dispose();
        }

        public SpecialWindow(UserControl userControl)
        {
            InitializeComponent();
            WindowStyle = WindowStyle.None;
            Height = userControl.Height * 2;
            Width = userControl.Width + 20;
            controlHolder.Children.Add(userControl);
        }

        private void Browser_DocumentLoadedInFrameEvent(object sender, DotNetBrowser.Events.FrameLoadEventArgs e)
        {
            if (checkRegex.Matches(e.Browser.URL).Count != 0)
            {
                Dictionary<String, String> uriParams = NetHelper.GetUriFields(new Uri(e.Browser.URL));

                if (uriParams.ContainsKey("error"))
                {
                    parameters["error"] = "true";
                    CloseWindow();
                    return;
                }

                for (int i = 0; i < parameters.Count; i++)
                {
                    for (int j = 0; j < uriParams.Count; j++)
                    {
                        if (parameters.ContainsKey(uriParams.ElementAt(j).Key))
                        {
                            parameters[uriParams.ElementAt(j).Key] = uriParams.ElementAt(j).Value;
                        }
                    }
                }

                CloseWindow();
            }
        }

        public void CloseWindow()
        {
            Process.GetCurrentProcess().CloseMainWindow();
        }
    }
}