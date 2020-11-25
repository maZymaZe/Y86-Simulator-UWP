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
using System.Collections.ObjectModel;

// https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x804 上介绍了“空白页”项模板

namespace r1
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        ObservableCollection<string> SourceList = new ObservableCollection<string>();
        ObservableCollection<string> MemoryList_s = new ObservableCollection<string>();
        bool IsPause = false;
        string[] RealSource;
        string PlayButtomTag = "";
        string ZF = "0";
        Windows.Storage.StorageFile SourceFile;
        string SourceText;

        //****************************************************************************
        const int RAX = 0, RCX = 1, RDX = 2, RBX = 3, RSP = 4, RBP = 5, RSI = 6, RDI = 7, R8 = 8, R9 = 9, R10 = 10, R11 = 11, R12 = 12, R13 = 13, R14 = 14, None = 15;

        int[] RegisterValue = new int[16];
        int F_predPC;
        string F_predPC_s, F_stat;
        string[] StatCollection = new string[] {
            "AOK", "HLT", "ADR","INS"
        };
        string[][] FunctionCollection=new string[][]{
            new string[]{"halt"},
            new string[] { "nop" },
            new string[]{"rrmovq","cmovle","cmovl","cmove","cmovne","cmovge","cmovg"},
            new string[] { "irmovq" },
            new string[] { "rmmovq" },
            new string[] { "mrmovq" },
            new string[] { "addq","subq","andq","xorq" },
            new string[] { "jmp","jle","jl","je","jne","jge","jg" },
            new string[] { "call" },
            new string[] { "ret" },
             new string[] { "pushq" },
            new string[] { "popq" }
        };


        //****************************************************************************

        public MainPage()
        {
            this.InitializeComponent();
            preset();
           
          
            

        }
        private void preset()
        {
            StatCollection[0] = "AOK";
            StatCollection[1] = "HLT";
            StatCollection[2] = "ADR";
            StatCollection[3] = "INS";
            SourceText = "NO INPUT YET";
            DataContext = this;
            this.MemoryListView.ItemsSource = MemoryList_s;
            this.SourceListView.ItemsSource = SourceList;
            SourceList.Add("NO INPUT YET");
            MemoryList_s.Add("0000:00000000");
            MemoryList_s.Add("0004:00000000");
            MemoryList_s.Add("0008:00000000");
            MemoryList_s.Add("000c:00000000");
            MemoryList_s.Add("0010:00000000");
            MemoryList_s.Add("0014:00000000");
            MemoryList_s.Add("0018:00000000");
            MemoryList_s.Add("001c:00000000");
            MemoryList_s.Add("0020:00000000");
            MemoryList_s.Add("0024:00000000");
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
                RealSource=SourceText.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                for(int i = 1; i <= RealSource.Length; i++)
                {
                    SourceList.Add(i.ToString("D4")+"|    "+RealSource[i-1]);
                }
            }
            this.SourceListView.SelectedIndex = 0;
            WorkCompletedForSource();
        }
        private Deferral RefreshCompletionDeferralForSource
        {
            get;
            set;
        }
        private Deferral RefreshCompletionDeferralForMemory
        {
            get;
            set;
        }
        private void SourceViewer_RefreshRequested(RefreshContainer sender, RefreshRequestedEventArgs args)
        {
            //Do some work to show new Content! Once the work is done, call RefreshCompletionDeferral.Complete()
            this.RefreshCompletionDeferralForSource = args.GetDeferral();
           // this.DoWork();
        }
        private void MemoryViewer_RefreshRequested(RefreshContainer sender, RefreshRequestedEventArgs args)
        {
            //Do some work to show new Content! Once the work is done, call RefreshCompletionDeferral.Complete()
            this.RefreshCompletionDeferralForMemory = args.GetDeferral();
            // this.DoWork();
        }

        private void WorkCompletedForSource()
        {
            if (this.RefreshCompletionDeferralForSource != null)
            {
                this.RefreshCompletionDeferralForSource.Complete();
                this.RefreshCompletionDeferralForSource.Dispose();
                this.RefreshCompletionDeferralForSource = null;
            }
        }
        private void WorkCompletedForMemory()
        {
            if (this.RefreshCompletionDeferralForMemory != null)
            {
                this.RefreshCompletionDeferralForMemory.Complete();
                this.RefreshCompletionDeferralForMemory.Dispose();
                this.RefreshCompletionDeferralForMemory = null;
            }
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            SourceText = null;
            SourceList.Clear();
            RealSource = null;
            WorkCompletedForSource();
            WorkCompletedForMemory();
        }
        private void PlayButtom_Click(object sender, RoutedEventArgs e)
        {
            IsPause = !IsPause;
            if (!IsPause) { 
            this.PlayButtom.Icon = new SymbolIcon((Symbol)57603);
                this.PlayButtom.Label = "Pause";
            }
            else
            {
                this.PlayButtom.Icon = new SymbolIcon((Symbol)57602);
                this.PlayButtom.Label = "Play";
            }

        }
    }
}
