using System;
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

// https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x804 上介绍了“空白页”项模板

namespace r1
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        List<string> MemoryData = new List<string>();
        string ZF = "0";
        public MainPage()
        {
            this.InitializeComponent();
            DataContext = this;
            this.Memory.ItemsSource = MemoryData;
            MemoryData.Add("0000:00000000");
            MemoryData.Add("0004:00000000");
            MemoryData.Add("0008:00000000");
            MemoryData.Add("000c:00000000");
            MemoryData.Add("0010:00000000");
            MemoryData.Add("0014:00000000"); 
            MemoryData.Add("0018:00000000"); 
            MemoryData.Add("001c:00000000"); 
            MemoryData.Add("0020:00000000");
            MemoryData.Add("0024:00000000");
        }
    }
}
