using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace r1
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class AboutPage : Page
    {
        public AboutPage()
        {
            this.InitializeComponent();
            this.AboutContent.Text = "Y86 Simulator v2.0.0\r\ndeveloer:maze&george_plover\r\ncontacts:782618517maze@gmail.com&19307130237@fudan.edu.cn\r\n2020.12.17";   
        }
        private void NavigationView_SelectionChanged(object sender, NavigationViewSelectionChangedEventArgs args)
        {
            /* NOTE: for this function to work, every NavigationView must follow the same naming convention: nvSample# (i.e. nvSample3),
            and every corresponding content frame must follow the same naming convention: contentFrame# (i.e. contentFrame3) */
            // Get the sample number
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
            else if (selectedItemTag == "AboutPage")
            {
                rootFrame.Navigate(typeof(AboutPage));
            }            
        }
    }
}