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
        List<string> SourceList = new List<string>();
        string[] RealSourse;
        string ZF = "0";
        Windows.Storage.StorageFile SourceFile;
        string SourceText;
        public MainPage()
        {
            this.InitializeComponent();
            SourceText = "NO INPUT YET";
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

        private async void OpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
              //TODO:FIX THIS
            picker.FileTypeFilter.Add(".txt");
            picker.FileTypeFilter.Add(".yo");
            picker.FileTypeFilter.Add(".cpp");
            SourceFile = await picker.PickSingleFileAsync();
            var stream = await SourceFile.OpenAsync(Windows.Storage.FileAccessMode.Read);
            ulong size = stream.Size;
            SourceList.Clear();
            using (var dataReader = new Windows.Storage.Streams.DataReader(stream))
            {
                uint numBytesLoaded = await dataReader.LoadAsync((uint)size);
                SourceText = dataReader.ReadString(numBytesLoaded);
                RealSourse=SourceText.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                for(int i = 1; i <= RealSourse.Length; i++)
                {
                    SourceList.Add(i.ToString("D4")+"|    "+RealSourse[i-1]);
                }
            }
            this.Source_Viewer.ItemsSource = SourceList;
            this.Source_Viewer.SelectedIndex = 0;
           
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            SourceList.Add("???");
        }
    }
}
