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
        bool IsPause = false,SourceIsLoaded=false;
        string[] RealSource;
        string PlayButtomTag = "";
        Windows.Storage.StorageFile SourceFile;
        string SourceText;
        DispatcherTimer Timer=new DispatcherTimer();



        //****************************************************************************
        const int RAX = 0, RCX = 1, RDX = 2, RBX = 3, RSP = 4, RBP = 5, RSI = 6, RDI = 7, R8 = 8, R9 = 9,
            R10 = 10, R11 = 11, R12 = 12, R13 = 13, R14 = 14, NONE = 15;

        long[] RegisterValue = new long[16];
        long CLOCK=0;

        long F_predPC, f_SelectPC, f_pc, f_Split, f_icode, f_ifun, f_Align, f_NeedvalC, f_Needregids, f_valP, f_valC, f_PredictPC;
        string F_predPC_s, f_stat, f_rA, f_rB;
        bool f_imem_error;
        bool F_stall,F_bubble;


        string D_stat, d_stat, D_instr, d_instr,D_rA,d_srcA,D_rB,d_srcB, d_dstE, d_dstM;
        long D_valC, d_valC, D_valP, d_valP, d_icode, d_ifun, D_icode, D_ifun;
        long d_rvalA, d_rvalB, d_valA, d_valB;
        bool D_stall,D_bubble;

        string ProgramStat;

      

        private void SpeedSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            this.Timer.Interval = new TimeSpan(0,0,0,0,(int)(1000/SpeedSlider.Value));
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (ProgramStat == "AOK"&&SourceIsLoaded) PipelineWork();
        }

        string E_stat, e_stat, E_instr, e_instr, E_dstE, e_dstE, E_dstM, e_dstM, E_srcA, e_srcA, E_srcB, e_srcB;
        long E_valA, e_valA, E_valB, e_valB, E_valC, e_valC, e_ALUfun, e_ALUA, e_ALUB, e_valE, E_icode, e_icode, E_ifun, e_ifun;

      

        bool e_setCC, ZF, SF, OF, e_Cnd, E_stall, E_bubble;

        string M_stat, m_stat, M_instr, m_instr,M_dstE,M_dstM,m_dstE,m_dstM;
        long M_valE, m_valE, M_valA, m_valA, m_Addr, M_icode, m_icode, M_ifun, m_ifun, m_valM;
        bool m_dmem_error, M_Cnd, m_write, m_read, M_stall, M_bubble;

        string W_stat, W_instr,W_dstE,W_dstM;
        long W_icode, W_ifun, W_valE, W_valM,w_icode,w_ifun;
        bool W_stall,W_bubble;


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
        Hashtable HexHash = new Hashtable()
        {
            { '0',0},{'1',1 },{'2',2},{'3',3},{'4',4},{'5',5},{'6',6},{'7',7},{'8',8},{'9',9},{'A',10},{'a',10},{'b',11},{'B',11},{'C',12},{'c',12},{'D',13},{'d',13},{'E',14},{'e',14},{'F',15},{'f',15}
        };

        char[] MemoryBlock = new char[1 << 20];
        char[] InstructionBlock = new char[1 << 20];
        long InstrPointer=0;
        long DataEntry, DataExit;
        //****************************************************************************

        //****************************************************************************          REGISTER
        private void ReadRegister()
        {
            //TODO:None
            
            d_rvalA = RegisterValue[(int)RegisterHash[d_srcA]];
            d_rvalB = RegisterValue[(int)RegisterHash[d_srcB]];
            if (d_srcA == "NONE")
                d_rvalA = 0;
            if (d_srcB == "NONE")
                d_rvalB = 0;
        }
        private void WriteRegister()
        {
            if (W_dstM != "None") { RegisterValue[(int)RegisterHash[W_dstM]] = W_valM; }
            if(W_dstE != "None"){ RegisterValue[(int)RegisterHash[W_dstE]] = W_valE; }
        }

        //****************************************************************************
        //****************************************************************************         ALU

        private void Alu()
        {
            switch (e_ALUfun) { 
                case 0:
                e_valE=e_ALUA+e_ALUB;
                    break;
                case 1:
                e_valE =e_ALUA-e_ALUB;
                    break;
                case 2:
                e_valE=e_ALUA&e_ALUB;
                    break;
                case 3:
                e_valE=e_ALUA^e_ALUB;
                    break;
            }
            if (e_setCC)
            {
                ZF = (e_valE == 0);
                SF = (e_valE < 0);
                if (e_ALUA >= 0 && e_ALUB >= 0 && e_valE < 0)
                {
                    OF = true;
                }else if (e_ALUA < 0 && e_ALUB < 0 && e_valE >= 0)
                {
                    OF = true;
                }
                else
                {
                    OF = false;
                }
            }
        }
        //****************************************************************************
        //****************************************************************************MEMORY
        private bool AddrIsLigal(long addr)
        {
            if(addr+8>(1<<20) || addr<0)return false;
            return true;
        }
        
        private void MemoryRead()
        {
            if(!AddrIsLigal(m_Addr))
            {
                m_dmem_error=true;
                return;
            }
            DataExit = Get8Bytes(m_Addr);
        }
        private void MemoryWrite()
        {
            if(!AddrIsLigal(m_Addr))
            {
                m_dmem_error=true;
                return;
            }
            Write8Bytes(m_Addr);
        }
        private long Get8Bytes(long addr)
        {
            long ret = 0;
            for(int i = 0; i < 8; i++)
            {
                ret = (ret << 8) + (long)MemoryBlock[addr + i];
            }
            return ret;
        }
        private void Write8Bytes(long addr)
        {
            long tmp = DataEntry;
            for(int i = 7; i >= 0; i--)
            {
                MemoryBlock[addr + i] = (char)(tmp & 0xff);
                tmp >>= 8;
            }
        }
        //****************************************************************************
        public MainPage()
        {
            this.InitializeComponent();
            this.Timer.Tick += new EventHandler<object>(this.Timer_Tick);

            Preset();
        }
        private void Timer_Tick(object sender, object e)
        { 
            //TODO
            if(!IsPause&&SourceIsLoaded&&ProgramStat=="AOK") PipelineWork();
        }




        private void PipelineWork()
        {
            ControlLogic();
            CLOCK++;
            RegUpdate();
            Fetch();
            Decode();
            Execute();
            Memory();
            WriteBack();
            Forward();
            GUIUpdate();
        }
        private void GUIUpdate()
        {
            this.Finstr.Text = FunctionCollection[f_icode][f_ifun];
            F_predPC_s = F_predPC.ToString("H16");
            this.DIinstr.Text= FunctionCollection[f_icode][f_ifun];
            this.EIinstr.Text = FunctionCollection[d_icode][d_ifun];
            this.DSinstr.Text = FunctionCollection[D_icode][D_ifun];
            this.MIinstr.Text = FunctionCollection[e_icode][e_ifun];
            this.ESinstr.Text = FunctionCollection[E_icode][E_ifun];
            this.WIinstr.Text = FunctionCollection[m_icode][m_ifun];
            this.MSinstr.Text = FunctionCollection[M_icode][M_ifun];
            this.WSinstr.Text = FunctionCollection[W_icode][W_ifun];
            
        }

        private void RegUpdate()
        {
            if(!F_stall && !F_bubble)
            {
                F_predPC=f_PredictPC;
            }
            
            if(!D_stall && !D_bubble)
            {
                D_stat=f_stat;D_icode=f_icode;D_ifun=f_ifun;D_rA=f_rA;D_rB=f_rB;D_valC=f_valC;D_valP=f_valP;
            }
            else if(D_bubble)
            {
                D_stat="AOK";D_icode=1;D_ifun=0;D_rA="NONE";D_rB="NONE";
            }

            if(!E_stall && !E_bubble)
            {
                E_stat=d_stat;E_icode=d_icode;E_ifun=d_ifun;E_valC=d_valC;E_valA=d_valA;E_valB=d_valB;E_dstE=d_dstE;E_dstM=d_dstM;E_srcA=d_srcA;E_srcB=d_srcB;
            }
            else if(D_bubble)
            {
                E_stat="AOK";E_icode=1;E_ifun=0;E_dstE=E_dstM=E_srcA=E_srcB="NONE";
            }

            if(!M_stall && !M_bubble)
            {
                M_stat=e_stat;M_icode=e_icode;M_ifun=e_ifun;M_Cnd=e_Cnd;M_valE=e_valE;M_valA=e_valA;M_dstE=e_dstE;M_dstM=e_dstM;
            }

            if(!W_stall && !W_bubble)
            {
                W_stat=m_stat;W_icode=m_icode;W_valE=m_valE;W_valM=m_valM;W_dstE=m_dstE;W_dstM=m_dstM;
            }

        }
        private void ControlLogic()
        {
            F_stall= ( ((E_icode==5||E_icode==11) && (E_dstM==d_srcA || E_dstM==d_srcB))||(D_icode==9 || E_icode==9 || M_icode == 9) );
            F_bubble=false;

            D_stall= ((E_icode==5||E_icode==11) && (E_dstM==d_srcA || E_dstM==d_srcB));
            D_bubble=( ( E_icode==7 && E_ifun>0 && !e_Cnd) || ( !((E_icode==5||E_icode==11) && (E_dstM==d_srcA || E_dstM==d_srcB)) && (D_icode==9 || E_icode==9 || M_icode == 9) ) );

            E_stall=false;
            E_bubble= ( E_icode==7 && E_ifun>0 && !e_Cnd) || ((E_icode==5||E_icode==11) && (E_dstM==d_srcA || E_dstM==d_srcB));

            M_stall=false;
            M_bubble=false;

            W_stall=false;
            W_bubble=false;
        }
        
        


        private long Trans8Bytes(long index){
            long ret=0;
            for(int i=0;i<8;i++){
                ret|=(long)(InstructionBlock[index+i]<<(i<<2));
            }
            return ret;            
        }
        private void Fetch()
        {
            if(M_icode==7 && M_ifun<=6 && M_ifun > 0 && !M_Cnd)
            {
                f_pc = M_valA;
            }
            else if(M_icode==9 && M_ifun==0)
            {
                f_pc = W_valM;
            }
            else
            {
                f_pc = F_predPC;
            }
            if (f_pc < 0 || f_pc > InstrPointer)
            {
                f_imem_error = true;
            }
            f_icode = InstructionBlock[f_pc];
            f_ifun = f_icode & 0xf;
            f_icode = (f_icode >> 4) & 0xf;

            
            if ((f_icode==0 || f_icode==1) && f_ifun==0)
            {
                f_rA = "NONE";f_rB = "NONE";f_valP = f_pc + 1;f_PredictPC = f_valP;
            }
            else if(f_icode==2&&f_ifun<=6&&f_ifun>=0)
            {
                f_rA = RegisterCollection[(InstructionBlock[f_pc + 1] >> 4) & 0xf];
                f_rB = RegisterCollection[InstructionBlock[f_pc + 1] & 0xf];
                f_valP = f_pc + 2; f_PredictPC = f_valP;
            }
            else if(f_icode==3&&f_ifun==0 && ((InstructionBlock[f_pc + 1] >> 4) & 0xf)==0xf)
            {
                f_rA = RegisterCollection[(InstructionBlock[f_pc + 1] >> 4) & 0xf];
                f_rB = RegisterCollection[InstructionBlock[f_pc + 1] & 0xf];
                f_valC = Trans8Bytes(f_pc + 2);
                f_valP = f_pc + 10; f_PredictPC = f_valP;
            }
            else if((f_icode==4||f_icode==5)&&f_ifun==0)
            {
                f_rA = RegisterCollection[(InstructionBlock[f_pc + 1] >> 4) & 0xf];
                f_rB = RegisterCollection[InstructionBlock[f_pc + 1] & 0xf];
                f_valC = Trans8Bytes(f_pc + 2);
                f_valP = f_pc + 10; f_PredictPC = f_valP;
            }
            else if(f_icode==6&&f_ifun>=0&&f_ifun<=3)
            {
                f_rA = RegisterCollection[(InstructionBlock[f_pc + 1] >> 4) & 0xf];
                f_rB = RegisterCollection[InstructionBlock[f_pc + 1] & 0xf];
                f_valP = f_pc + 2; f_PredictPC = f_valP;
            }
            else if((f_icode==7&&f_ifun>=0&&f_ifun<=6) || (f_icode == 8 && f_ifun == 0))
            {
                f_rA = "NONE";
                f_rB = "NONE";
                f_valC = Trans8Bytes(f_pc + 1);
                f_valP = f_pc + 9; f_PredictPC = f_valC;
            }
            else if(f_icode==9&&f_ifun==0)
            {
                f_rA = "NONE"; f_rB = "NONE"; f_valP = f_pc + 1; f_PredictPC = f_valP;
            }
            else if((f_icode==10||f_icode==11)&&f_ifun==0&&(InstructionBlock[f_pc + 1] & 0xf)==0xf)
            {
                f_rA = RegisterCollection[(InstructionBlock[f_pc + 1] >> 4) & 0xf];
                f_rB = RegisterCollection[InstructionBlock[f_pc + 1] & 0xf];
                f_valP = f_pc + 2; f_PredictPC = f_valP;
            }
            else
            {
                //TODO:ICODEERROR
            }
            //TODO:GUI
        }
        private void Decode()
        {
            if(D_icode == 0||D_icode==1||D_icode==4||D_icode==7)
            {
                d_dstE = "NONE";d_dstM = "NONE";d_srcA = D_rA;d_srcB = D_rB;
            }
            else if(D_icode==2||D_icode==3)
            {
                d_dstE = D_rB; d_dstM = "NONE"; d_srcA = D_rA; d_srcB = "NONE"; 
            }
            else if(D_icode==5)
            {
                d_dstE = "NONE"; d_dstM = D_rA; d_srcA = "NONE"; d_srcB = D_rB; 
            }
            else if(D_icode==6)
            {
                d_dstE = D_rB; d_dstM = "NONE"; d_srcA = D_rA; d_srcB = D_rB; 
            }
            else if(D_icode==8)
            {
                d_dstE = "RSP"; d_dstM = "NONE"; d_srcA = "NONE"; d_srcB = "RSP";
            }
            else if(D_icode==9)
            {
                d_dstE = "RSP"; d_dstM = "NONE"; d_srcA = "RSP"; d_srcB = "RSP";
            }
            else if(D_icode==10)
            {
                d_dstE = "RSP"; d_dstM = "NONE"; d_srcA = D_rA; d_srcB = "RSP";
            }
            else if(D_icode==11)
            {
                d_dstE = "RSP"; d_dstM = D_rA; d_srcA = "RSP"; d_srcB = "RSP";
            }
            ReadRegister();
            d_valB = d_rvalB;
            if(D_icode==7||D_icode==8)
            {
                d_valA = D_valP;
            }
            else
            {
                d_valA = d_rvalA;
            }
            d_valC = D_valC;
            if(D_icode==8||D_icode==10)
            {
                d_valC=-8;
            }
            if(D_icode==9||D_icode==11)
            {
                d_valC=8;
            }
            d_icode = D_icode;
            d_ifun = D_ifun;
        }
        private void Execute()
        {
            if((E_icode==2||E_icode==7) && E_ifun>=1 && E_ifun<=6)
            {
                switch(E_ifun)
                {
                    case 1:
                        e_Cnd = (SF ^ OF) | ZF;
                        break;
                    case 2:
                        e_Cnd = SF ^ OF;
                        break;
                    case 3:
                        e_Cnd = ZF;
                        break;
                    case 4:
                        e_Cnd = !ZF;
                        break;
                    case 5:
                        e_Cnd = !(SF ^ OF);
                        break;
                    case 6:
                        e_Cnd = (!(SF ^ OF))&(!ZF);
                        break;
                }
            }
            if(E_icode==6)
            {
                //TODO: unexpect control
                e_setCC=true;
                e_ALUfun=E_icode;
            }
            else
            {
                e_setCC=false;
                e_ALUfun=0;
            }
            e_ALUB = E_valB;
            if(E_icode<=2||E_icode==6)
            {
                e_ALUA = E_valA;
            }
            else
            {
                e_ALUA = E_valC;
            }
            Alu();
            if(E_icode==2 && E_ifun>0 && !e_Cnd)
            {
                e_dstE = "NONE";
            }
            else
            {
                e_dstE = E_dstE;
            }
            e_dstM = E_dstM;
            e_valA = E_valA;
            e_stat = E_stat;
            e_icode = E_icode;
        }
        private void Memory()
        {
            if(M_icode==4||M_icode==8||M_icode==10)
            {
                DataEntry = M_valA;
                m_Addr = M_valE;
                MemoryWrite();
            }
            else if(M_icode==5)
            {
                DataExit = M_valE;
                MemoryRead();
            }
            else if (M_icode == 9||M_icode==11)
            {
                DataExit = M_valA;
                MemoryRead();
            }
            m_stat = M_stat;
            //TODO:stat Error
            m_icode = M_icode;
            m_valE = M_valE;
            m_valM = DataExit;
            m_dstE = M_dstE;
            m_dstM = M_dstM;
        }
        private void WriteBack()
        {
            if(W_icode==2||W_icode==3||W_icode==5||W_icode==6||W_icode>=8)
            {
                WriteRegister();
            }
        }
        private void Forward()
        {
            if (d_srcA == e_dstE && e_dstE != "NONE")
                d_valA = e_valE;
            else if (d_srcA == M_dstM && M_dstM != "NONE")
                d_valA = m_valM;
            else if (d_srcA == M_dstE && M_dstE != "NONE")
                d_valA = M_valE;
            else if (d_srcA == W_dstM && W_dstM != "NONE")
                d_valA = W_valM;
            else if (d_srcA == W_dstE && W_dstE != "NONE")
                d_valA = W_valE;
        }

        private void Preset()
        {
            SourceText = "NO INPUT YET";
            DataContext = this;
            this.MemoryListView.ItemsSource = MemoryList_s;
            this.SourceListView.ItemsSource = SourceList;
            SourceIsLoaded = false;
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
                RealSource=SourceText.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
                for(int i = 1; i <= RealSource.Length; i++)
                {
                    SourceList.Add(i.ToString("D4")+"|    "+RealSource[i-1]);
                }
            }
            this.SourceListView.SelectedIndex = 0;
            WorkCompletedForSource();
            LoadInstructions();
        }
        private void LoadInstructions()
        {
            for (int i = 0; i < RealSource.Length; i++)
            {
                bool flag = false;
                for (int j = 0; j < RealSource[i].Length; j++)
                {
                    if (!flag && RealSource[i][j] == ':')
                    {
                        flag = true;
                    }
                    else if (flag && MyIsDigit(RealSource[i][j]))
                    {
                        InstructionBlock[InstrPointer++] = (char)(((int)HexHash[RealSource[i][j]] << 4) | (int)HexHash[RealSource[i][j + 1]]);
                        j++;
                    }
                    else if (RealSource[i][j] == '|')
                    {
                        break;
                    }
                }
            }
            SourceIsLoaded = true;
        }
        private bool MyIsDigit(char x)
        {
            return (x >= '0' && x <= '9') || (x >= 'A' && x <= 'F') || (x >= 'a' && x <= 'f');
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
            CLOCK = 0;
            IsPause = true;
            Preset();
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
      /*
            long TimeSinceLastUpdate = 0, Pretime;

            while (true)
            {
                Pretime = (DateTime.Now.ToUniversalTime().Ticks - 621355968000000000) / 10000;
                while (!IsPause && ISourceIsLoaded)
                {
                    TimeSinceLastUpdate += (DateTime.Now.ToUniversalTime().Ticks - 621355968000000000) / 10000 - Pretime;
                    Pretime = (DateTime.Now.ToUniversalTime().Ticks - 621355968000000000) / 10000;
                    while (TimeSinceLastUpdate > 1000 / this.SpeedSlider.Value)
                    {
                        TimeSinceLastUpdate -= (long)(1000 / this.SpeedSlider.Value);
                        PipelineWork();
                        
                    }
                }
            }
      */
        
    }
}
