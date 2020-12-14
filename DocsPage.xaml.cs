using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Collections.ObjectModel;
using System.Windows;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace r1
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class DocsPage : Page
    {
        public DocsPage()
        {
            this.InitializeComponent();
        }
        private void NavigationView_SelectionChanged(object sender, NavigationViewSelectionChangedEventArgs args)
        {
            /* NOTE: for this function to work, every NavigationView must follow the same naming convention: nvSample# (i.e. nvSample3),
            and every corresponding content frame must follow the same naming convention: contentFrame# (i.e. contentFrame3) */

            // Get the sample number

            if (args.IsSettingsSelected)
            {
                //  contentFrame.Navigate(typeof(SampleSettingsPage));
            }
            else
            {
                Frame rootFrame = Window.Current.Content as Frame;
                var selectedItem = (NavigationViewItem)args.SelectedItem;
                string selectedItemTag = ((string)selectedItem.Tag);
                if (selectedItemTag == "MainPage")
                {
                    rootFrame.Navigate(typeof(MainPage));
                }
                else if (selectedItemTag == "DocsPage")
                {
                    rootFrame.Navigate(typeof(DocsPage));
                }
            }
        }
    }
}
