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
        const int RAX = 0, RCX = 1, RDX = 2, RBX = 3, RSP = 4, RBP = 5, RSI = 6, RDI = 7, R8 = 8, R9 = 9,
            R10 = 10, R11 = 11, R12 = 12, R13 = 13, R14 = 14, NONE = 15;

        long[] RegisterValue = new long[16];

        long F_predPC, f_SelectPC, f_pc, f_Split, f_icode, f_ifun, f_Align, f_imem_error, f_NeedvalC, f_Needregids, f_valP, f_valC, f_PredictPC;
        string F_predPC_s, F_stat;


        string D_stat, d_stat, D_instr, d_instr,D_rA,d_rA,D_rB,d_rB;
        long D_valC, d_valC, D_valP, d_valP, d_icode, d_ifun;
        long d_rvalA, d_rvalB;

        string E_stat, e_stat, E_instr, e_instr, E_dstE, e_dstE, E_dstM, e_dstM, E_srcA, e_srcA, E_srcB, e_srcB;
        long E_valA, e_valA, E_valB, e_valB, E_valC, e_valC;

        string M_stat, m_stat, M_instr, m_instr,M_dstE,M_dstM,m_dstE,m_dstM;
        long M_valE, m_valE, M_valA, m_valA;

        string W_stat, w_stat, W_instr, w_instr,W_dstE,W_dstM;
        long W_icode, W_valE, W_valM;

        string[] StatCollection = new string[] {
            "AOK", "HLT", "ADR","INS"
        };
        string[] RegisterCollection = new string[]
       {
            "RAX","RCX","RDX","RBX","RSP","RBP","RSI","RDI","R8","R9","R10","R11","R12","R13","R14","NONE"
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

        Hashtable RegisterHash = new Hashtable() {
            {"RAX",0 },{"RCX",1},{"RDX",2},{"RBX",3},{"RSP",4},{"RBP",5},{"RSI",6},{"RDI",7},{"R8",8},{"R9",9},{"R10",10},{"R11",11},{"R12",12},{"R13",13},{"R14",14},{"NONE",15}
        };
        //****************************************************************************

        //****************************************************************************          REGISTER
        private void ReadRegister()
        {   
            //TODO:None
            d_rvalA = RegisterValue[(int)RegisterHash[d_rA]];
            d_rvalB = RegisterValue[(int)RegisterHash[d_rB]];
        }
        private void WriteRegister()
        {
            if (W_dstM != "None") { RegisterValue[(int)RegisterHash[W_dstM]] = W_valM; }
            if(W_dstE != "None"){ RegisterValue[(int)RegisterHash[W_dstE]] = W_valE; }
        }
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
