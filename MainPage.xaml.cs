using System;
using System.Collections;
using System.Collections.Generic;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using System.Collections.ObjectModel;
using Windows.Storage;
using Windows.Media.Playback;
using Windows.Media.Core;

// https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x804 上介绍了“空白页”项模板

namespace r1
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        StorageFile SourceFile;
        string SourceText;
        ObservableCollection<string> SourceList = new ObservableCollection<string>();
        ObservableCollection<string> MemoryList_s = new ObservableCollection<string>();
        char[] MemoryBlock = new char[1 << 20];
        char[] MemoryBlockCopy = new char[1 << 20];
        string[] RealSource;

        long MaxAddr,InstrPointer = 0,DataEntry, DataExit;

        bool IsPause = false, SourceIsLoaded = false;
        bool Endflag = true, UndoFlag = false;
        string XorD = "X16";

        DispatcherTimer Timer = new DispatcherTimer();
        MediaPlayer MusicPlayer = new MediaPlayer();

        long[] RegisterValue = new long[16];
        long CLOCK = 0;
        long PenaltyCnt = 0;

        string ProgramStat;
        double CPI;

        long F_predPC, f_pc, f_icode, f_ifun, f_valP, f_valC, f_PredictPC;
        string F_predPC_s, f_stat, f_rA, f_rB;
        bool F_stall, F_bubble;

        string D_stat, d_stat, D_rA, d_srcA, D_rB, d_srcB, d_dstE, d_dstM;
        long D_valC, d_valC, D_valP, d_icode, d_ifun, D_icode, D_ifun;
        long d_rvalA, d_rvalB, d_valA, d_valB;
        bool D_stall, D_bubble;

        string E_stat, e_stat, E_dstE, e_dstE, E_dstM, e_dstM, E_srcA, E_srcB;
        long E_valA, e_valA, E_valB, E_valC, e_ALUfun, e_ALUA, e_ALUB, e_valE, E_icode, e_icode, E_ifun, e_ifun;
        bool e_setCC, ZF, SF, OF, e_Cnd, E_stall, E_bubble;
     
        string M_stat, m_stat,M_dstE,M_dstM,m_dstE,m_dstM;
        long M_valE, m_valE, M_valA, m_Addr, M_icode, m_icode, M_ifun, m_ifun, m_valM;
        bool m_dmem_error, M_Cnd, M_stall, M_bubble;

        string W_stat,W_dstE,W_dstM;
        long W_icode, W_ifun, W_valE, W_valM;
        bool W_stall,W_bubble;

        Hashtable LineHash = new Hashtable();
        Hashtable AddrHash = new Hashtable();
        HashSet<long> BreakPoints = new HashSet<long>();
        readonly string[] RegisterCollection = new string[]
       {
            "RAX","RCX","RDX","RBX","RSP","RBP","RSI","RDI","R8","R9","R10","R11","R12","R13","R14","NONE"
       };
        readonly string[][] FunctionCollection=new string[][]{
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
            new string[] { "popq" },
            new string[] { "iaddq" }
        };
        readonly Hashtable RegisterHash = new Hashtable() {
            {"RAX",0 },{"RCX",1},{"RDX",2},{"RBX",3},{"RSP",4},{"RBP",5},{"RSI",6},{"RDI",7},{"R8",8},{"R9",9},{"R10",10},{"R11",11},{"R12",12},{"R13",13},{"R14",14},{"NONE",15}
        };
        readonly Hashtable HexHash = new Hashtable()
        {
            { '0',0},{'1',1 },{'2',2},{'3',3},{'4',4},{'5',5},{'6',6},{'7',7},{'8',8},{'9',9},{'A',10},{'a',10},{'b',11},{'B',11},{'C',12},{'c',12},{'D',13},{'d',13},{'E',14},{'e',14},{'F',15},{'f',15}
        };
       
        //****************************************************************************

        //****************************************************************************          REGISTER
        private void ReadRegister()
        {
            d_rvalA = RegisterValue[(int)RegisterHash[d_srcA]];
            d_rvalB = RegisterValue[(int)RegisterHash[d_srcB]];
            if (d_srcA == "NONE")
                d_rvalA = 0;
            if (d_srcB == "NONE")
                d_rvalB = 0;
        }
        private void WriteRegister()
        {
            if(W_dstE != "NONE"){ RegisterValue[(int)RegisterHash[W_dstE]] = W_valE; }
            if (W_dstM != "NONE") { RegisterValue[(int)RegisterHash[W_dstM]] = W_valM; }
            RegisterChange();
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
                e_valE =e_ALUB-e_ALUA;
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
                if (e_ALUfun != 1) { 
                    OF = (e_ALUA < 0 && e_ALUB < 0 && e_valE >= 0)||(e_ALUA >= 0 && e_ALUB >= 0 && e_valE < 0);
                }
                else
                {
                    OF = (e_ALUB >= e_ALUA && e_valE < 0) || (e_ALUB <= e_ALUA && e_valE > 0);
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
            if(Endflag)
            {
                for(int i = 7; i >= 0; i--)
                {
                    ret = (ret << 8) + (long)MemoryBlock[addr + i];
                }
            }
            else
            {
                for(int i = 0; i <= 7; i++)
                {
                    ret = (ret << 8) + (long)MemoryBlock[addr + i];
                }
            }
            return ret;
        }
        private void Write8Bytes(long addr)
        {
            long tmp = DataEntry;
            if(Endflag)
            {
                for(int i=0;i<8;i++)
                {
                    MemoryBlock[addr + i] = (char)(tmp & 0xff);
                    tmp >>= 8;
                }
            }
            else
            {
                for(int i=0;i<8;i++)
                {
                    MemoryBlock[addr + i] = (char)(tmp & 0xff);
                    tmp >>= 8;
                }
            }
            MemoryViewSetup();
        }
        //****************************************************************************
        public MainPage()
        {
            this.InitializeComponent();
            MusicPlayer.Source = MediaSource.CreateFromUri(new Uri(@"ms-appx:///Assets/bgm.mp3"));
            this.Timer.Tick += new EventHandler<object>(this.Timer_Tick);
            this.MemoryListView.ItemsSource = MemoryList_s;
            this.SourceListView.ItemsSource = SourceList;

            FrameworkElement root = (FrameworkElement)Window.Current.Content;
            root.RequestedTheme = AppSettings.Theme;
            Preset();           
        }
        private void Timer_Tick(object sender, object e)
        { 
            if(!IsPause&&SourceIsLoaded&&ProgramStat=="AOK") PipelineWork();
            else
            {
                GUIUpdate();
                this.PlayButtom.Icon = new SymbolIcon((Symbol)57602);
                this.PlayButtom.Label = "Play";
                Timer.Stop();
                IsPause = true;
                this.DocsPageNavi.IsEnabled = true;
                this.AboutPageNavi.IsEnabled = true;
            }
        }
        private void PipelineWork()
        {

            this.EndSwitch.IsEnabled = false;
            CLOCK++;
            RegUpdate();
            if(BreakPoints.Contains(F_predPC))
            {
                StopPipe();
            }
            if(W_stat!="AOK")
            {
                ProgramStat = W_stat;
                this.TestOutput.Text += ProgramStat + "\r\n";
                GUIUpdate();
                return;
            }
            Fetch();
            Decode();
            Execute();
            Memory();
            WriteBack();
            Forward();
            ControlLogic();
            GUIUpdate();
        }
        private void GUIUpdate()
        {
            if (UndoFlag) return;
            F_predPC_s = F_predPC.ToString("X16");
            CPI = (CLOCK-4-PenaltyCnt>0)?1.0 * (CLOCK-4)/(CLOCK-4 - PenaltyCnt):1.0 ;
            this.Finstr.Text = (f_icode<FunctionCollection.Length && f_ifun<FunctionCollection[f_icode].Length) ? FunctionCollection[f_icode][f_ifun] : "UKI";
            this.DIinstr.Text = (f_icode < FunctionCollection.Length && f_ifun < FunctionCollection[f_icode].Length) ? FunctionCollection[f_icode][f_ifun] : "UKI";
            this.EIinstr.Text = (d_icode < FunctionCollection.Length && d_ifun < FunctionCollection[d_icode].Length) ? FunctionCollection[d_icode][d_ifun] : "UKI";
            this.DSinstr.Text = (D_icode < FunctionCollection.Length && D_ifun < FunctionCollection[D_icode].Length) ? FunctionCollection[D_icode][D_ifun] : "UKI";
            this.MIinstr.Text = (e_icode < FunctionCollection.Length && e_ifun < FunctionCollection[e_icode].Length) ? FunctionCollection[e_icode][e_ifun] : "UKI";
            this.ESinstr.Text = (E_icode < FunctionCollection.Length && E_ifun < FunctionCollection[E_icode].Length) ? FunctionCollection[E_icode][E_ifun] : "UKI";
            this.WIinstr.Text = (m_icode < FunctionCollection.Length && m_ifun < FunctionCollection[m_icode].Length) ? FunctionCollection[m_icode][m_ifun] : "UKI";
            this.MSinstr.Text = (M_icode < FunctionCollection.Length && M_ifun < FunctionCollection[M_icode].Length) ? FunctionCollection[M_icode][M_ifun] : "UKI";
            this.WSinstr.Text = (W_icode < FunctionCollection.Length && W_ifun < FunctionCollection[W_icode].Length) ? FunctionCollection[W_icode][W_ifun] : "UKI";
            ToggleButton[] registers = new ToggleButton[15] { Hrax, Hrcx, Hrdx, Hrbx, Hrsp, Hrbp, Hrsi, Hrdi, Hr8, Hr9, Hr10, Hr11, Hr12, Hr13, Hr14 };
            for(int i=0;i<15;i++)registers[i].Content= (RegisterValue[i]).ToString(XorD);

            this.DSvalC.Text = D_valC.ToString(XorD);
            this.DSvalP.Text = D_valP.ToString(XorD);
            this.DIvalC.Text = f_valC.ToString(XorD);
            this.DIvalP.Text = f_valP.ToString(XorD);
            this.ESvalC.Text = E_valC.ToString(XorD);
            this.ESvalA.Text = E_valA.ToString(XorD);
            this.ESvalB.Text = E_valB.ToString(XorD);
            this.EIvalC.Text = d_valC.ToString(XorD);
            this.EIvalA.Text = d_valA.ToString(XorD);
            this.EIvalB.Text = d_valB.ToString(XorD);
            this.MSvalA.Text = M_valA.ToString(XorD);
            this.MSvalE.Text = M_valE.ToString(XorD);
            this.MIvalA.Text = e_valA.ToString(XorD);
            this.MIvalE.Text = e_valE.ToString(XorD);
            this.WSvalM.Text = W_valM.ToString(XorD);
            this.WSvalE.Text = W_valE.ToString(XorD);
            this.WIvalM.Text = m_valM.ToString(XorD);
            this.WIvalE.Text = m_valE.ToString(XorD);

            Bindings.Update();
            if (SourceIsLoaded&&LineHash.Contains(F_predPC)){
                this.SourceListView.SelectedIndex = Convert.ToInt32(LineHash[F_predPC]);
                SourceListView.ScrollIntoView(SourceListView.SelectedItem);
            }
        }
        private void ControlLogic()
        {
            F_stall = (((E_icode == 5 || E_icode == 11) && (E_dstM == d_srcA || E_dstM == d_srcB)) || (D_icode == 9 || E_icode == 9 || M_icode == 9) || (D_icode == 0 || E_icode == 0 || M_icode == 0));
            F_bubble = false;

            D_stall = ((E_icode == 5 || E_icode == 11) && (E_dstM == d_srcA || E_dstM == d_srcB));
            D_bubble = ((E_icode == 7 && E_ifun > 0 && !e_Cnd) || (!((E_icode == 5 || E_icode == 11) && (E_dstM == d_srcA || E_dstM == d_srcB)) && (D_icode == 9 || E_icode == 9 || M_icode == 9)) || (D_icode == 0 || E_icode == 0 || M_icode == 0));

            E_stall = false;
            E_bubble = (E_icode == 7 && E_ifun > 0 && !e_Cnd) || ((E_icode == 5 || E_icode == 11) && (E_dstM == d_srcA || E_dstM == d_srcB)) || (m_stat!="AOK");

            M_stall = false;
            M_bubble = (m_stat != "AOK");

            PenaltyCnt += (E_bubble ? 1 : 0) | (D_bubble ? 1 : 0) | (M_bubble ? 1 : 0);

            W_stall = false;
            W_bubble = false;
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
            else if(E_bubble)
            {
                E_stat="AOK";E_icode=1;E_ifun=0;E_dstE=E_dstM=E_srcA=E_srcB="NONE";
            }

            if(!M_stall && !M_bubble)
            {
                M_stat=e_stat;M_icode=e_icode;M_ifun=e_ifun;M_Cnd=e_Cnd;M_valE=e_valE;M_valA=e_valA;M_dstE=e_dstE;M_dstM=e_dstM;
            }
            else if(M_bubble)
            {
                M_stat = "AOK"; M_icode = 1; M_ifun = 0; M_Cnd = false; M_dstE = M_dstM = "NONE";
            }

            if(!W_stall && !W_bubble)
            {
                W_stat=m_stat;W_icode=m_icode;W_ifun = m_ifun; W_valE=m_valE;W_valM=m_valM;W_dstE=m_dstE;W_dstM=m_dstM;
            }
            else if(W_bubble)
            {
                W_stat = "AOK"; W_icode = 1; W_dstE = W_dstM = "NONE";
            }

        }
        private long Trans8Bytes(long index){
            if(!AddrIsLigal(index))
            {
                f_stat = "ADR";
                return 0;
            }
            return Get8Bytes(index);            
        }
        private void Fetch()
        {
            f_stat = "AOK";
            if(M_icode==7 && M_ifun<=6 && M_ifun > 0 && !M_Cnd)
            {
                f_pc = M_valA;
            }
            else if(W_icode==9 && W_ifun==0)
            {
                f_pc = W_valM;
            }
            else
            {
                f_pc = F_predPC;
            }
            f_icode = MemoryBlock[f_pc];
            f_ifun = f_icode & 0xf;
            f_icode = (f_icode >> 4) & 0xf;

            
            if ((f_icode==0 || f_icode==1) && f_ifun==0)
            {
                f_rA = "NONE";f_rB = "NONE";f_valP = f_pc + 1;f_PredictPC = f_valP;
            }
            else if(f_icode==2&&f_ifun<=6&&f_ifun>=0)
            {
                f_rA = RegisterCollection[(MemoryBlock[f_pc + 1] >> 4) & 0xf];
                f_rB = RegisterCollection[MemoryBlock[f_pc + 1] & 0xf];
                if (f_rA == "NONE" || f_rB == "NONE")
                    f_stat = "INS";
                f_valP = f_pc + 2; f_PredictPC = f_valP;
            }
            else if(f_icode==3&&f_ifun==0 && ((MemoryBlock[f_pc + 1] >> 4) & 0xf)==0xf)
            {
                f_rA = RegisterCollection[(MemoryBlock[f_pc + 1] >> 4) & 0xf];
                f_rB = RegisterCollection[MemoryBlock[f_pc + 1] & 0xf];
                if (f_rB == "NONE")
                    f_stat = "INS";
                f_valC = Trans8Bytes(f_pc + 2);
                f_valP = f_pc + 10; f_PredictPC = f_valP;
            }
            else if((f_icode==4||f_icode==5)&&f_ifun==0)
            {
                f_rA = RegisterCollection[(MemoryBlock[f_pc + 1] >> 4) & 0xf];
                f_rB = RegisterCollection[MemoryBlock[f_pc + 1] & 0xf];
                if (f_rA == "NONE" || f_rB == "NONE")
                    f_stat = "INS";
                f_valC = Trans8Bytes(f_pc + 2);
                f_valP = f_pc + 10; f_PredictPC = f_valP;
            }
            else if(f_icode==6&&f_ifun>=0&&f_ifun<=3)
            {
                f_rA = RegisterCollection[(MemoryBlock[f_pc + 1] >> 4) & 0xf];
                f_rB = RegisterCollection[MemoryBlock[f_pc + 1] & 0xf];
                if (f_rA == "NONE" || f_rB == "NONE")
                    f_stat = "INS";
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
            else if((f_icode==10||f_icode==11)&&f_ifun==0&&(MemoryBlock[f_pc + 1] & 0xf)==0xf)
            {
                f_rA = RegisterCollection[(MemoryBlock[f_pc + 1] >> 4) & 0xf];
                f_rB = RegisterCollection[MemoryBlock[f_pc + 1] & 0xf];
                if (f_rA == "NONE")
                    f_stat = "INS";
                f_valP = f_pc + 2; f_PredictPC = f_valP;
            }
            else if(f_icode==12 && f_ifun==0 && ((MemoryBlock[f_pc + 1] >> 4) & 0xf)==0xf)
            {
                f_rA = RegisterCollection[(MemoryBlock[f_pc + 1] >> 4) & 0xf];
                f_rB = RegisterCollection[MemoryBlock[f_pc + 1] & 0xf];
                if (f_rB == "NONE")
                    f_stat = "INS";
                f_valC = Trans8Bytes(f_pc + 2);
                f_valP = f_pc + 10; f_PredictPC = f_valP;
            }
            else
            {
                f_stat = "INS";
            }

            if (f_icode == 0 && f_ifun == 0)
                f_stat = "HLT";
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
            else if(D_icode==12)
            {
                d_dstE = D_rB; d_dstM = "NONE"; d_srcA = "NONE"; d_srcB = D_rB;
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
            d_stat = D_stat;
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
            if(E_icode==6 || E_icode==12)
            {
                e_setCC=true;
                e_ALUfun=E_ifun;
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
            e_ifun = E_ifun;
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
                m_Addr = M_valE;
                MemoryRead();
            }
            else if (M_icode == 9||M_icode==11)
            {
                m_Addr = M_valA;
                MemoryRead();
            }
            m_stat = M_stat;
            if (m_dmem_error)
                m_stat = "ADR";         
            m_icode = M_icode;
            m_valE = M_valE;
            m_valM = DataExit;
            m_dstE = M_dstE;
            m_dstM = M_dstM;
            m_ifun = M_ifun;
            
        }
        private void WriteBack()
        {
            if(W_icode==2||W_icode==3||W_icode==5||W_icode==6||W_icode>=8)
            {
                WriteRegister();
            }
            if(W_stat == "HLT")
            {
                ProgramStat = "HLT";
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

            if (d_srcB == e_dstE && e_dstE != "NONE")
                d_valB = e_valE;
            else if (d_srcB == M_dstM && M_dstM != "NONE")
                d_valB = m_valM;
            else if (d_srcB == M_dstE && M_dstE != "NONE")
                d_valB = M_valE;
            else if (d_srcB == W_dstM && W_dstM != "NONE")
                d_valB = W_valM;
            else if (d_srcB == W_dstE && W_dstE != "NONE")
                d_valB = W_valE;
        }
        private void Preset()
        {
            SourceText = "NO INPUT YET";
            DataContext = this;
            Timer.Start();
            SourceIsLoaded = false;
            SourceList.Add("NO INPUT YET");
            WorkCompletedForMemory();
            WorkCompletedForSource();
            this.DocsPageNavi.IsEnabled = true;
            this.AboutPageNavi.IsEnabled = true;
        }
        private async void OpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker
            {
                ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail,
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary
            };
            picker.FileTypeFilter.Add(".yo");
            SourceFile = await picker.PickSingleFileAsync();
            if (SourceFile == null) return;
            var stream = await SourceFile.OpenAsync(FileAccessMode.Read);
            ulong size = stream.Size;
            SourceList.Clear();
            using (var dataReader = new Windows.Storage.Streams.DataReader(stream))
            {
                uint numBytesLoaded = await dataReader.LoadAsync((uint)size);
                SourceText = dataReader.ReadString(numBytesLoaded);
                RealSource=SourceText.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
            }
            this.SourceListView.SelectedIndex = 0;
            WorkCompletedForSource();
            LoadInstructions();
        }
        private void LoadInstructions()
        {
            MemoryBlockCopy = new char[1 << 20];
            long cnt = 0;
            MaxAddr = 0;
            LineHash.Clear();
            AddrHash.Clear();
            BreakPoints.Clear();
            for (int i = 0; i < RealSource.Length; i++)
            {
                bool flag = false,addrloadflag=false,ValidLineFlag=false;
                long target=0;               
                int pos=0;
                for (int j = 0; j < RealSource[i].Length; j++)
                {
                    if (!addrloadflag && RealSource[i][j] == 'x')
                    {
                        addrloadflag = true;
                    }
                    else if (addrloadflag&&!flag&&MyIsDigit(RealSource[i][j]))
                    {
                        target = target * 16 + (int)HexHash[RealSource[i][j]];
                    }
                    else if (!flag && RealSource[i][j] == ':'&&addrloadflag)
                    {
                        flag = true;
                        addrloadflag = false;
                        InstrPointer = target;
                        MaxAddr = MaxAddr < target ? target : MaxAddr;
                    }
                    else if (flag && MyIsDigit(RealSource[i][j]))
                    {
                        ValidLineFlag = true;
                        MemoryBlockCopy[InstrPointer++] = (char)(((int)HexHash[RealSource[i][j]] << 4) | (int)HexHash[RealSource[i][j + 1]]);
                        j++;
                    }
                    
                    if (RealSource[i][j] == '|')
                    {
                        pos = j;
                        break;
                    }
                }
                if (flag&&ValidLineFlag)
                {
                    SourceList.Add(cnt.ToString("D4") + "|    " +RealSource[i].Substring(0,pos));
                    LineHash.Add(target,cnt);
                    AddrHash.Add(cnt,target);
                    cnt++;
                }
            }
            this.TestOutput.Text = "File is loaded\r\n" ;
            SourceIsLoaded = true;
            SourceInit();
            MaxAddr = MaxAddr / 4 * 4 + 32;
            MemoryViewSetup();
        }
        private void SourceInit()
        {
            MemoryBlock = (char[])MemoryBlockCopy.Clone();
            ProgramStat = "AOK";
            f_stat=D_stat = "AOK"; f_icode=D_icode = 1; f_ifun=D_ifun = 0; f_rA=D_rA = "NONE"; f_rB=D_rB = "NONE";
            d_stat=E_stat = "AOK"; d_icode=E_icode = 1; d_ifun=E_ifun = 0; d_dstE = d_dstM = d_srcA = d_srcB = E_dstE = E_dstM = E_srcA = E_srcB = "NONE";
            e_stat=M_stat = "AOK"; e_icode=M_icode = 1; e_ifun=M_ifun = 0; e_Cnd=M_Cnd = false;e_dstE = e_dstM =M_dstE =  M_dstM = "NONE";
            m_stat=W_stat = "AOK"; m_icode=W_icode = 1; m_ifun=W_ifun=0; m_dstE = m_dstM = W_dstE = W_dstM = "NONE";
            f_PredictPC = 0;
            F_predPC = 0;
            F_predPC_s = F_predPC.ToString("X16");
            f_valP= f_valC = 0;
            D_valC = d_valC = D_valP = d_valA = d_valB;
            E_valA = e_valA = E_valB = E_valC = 0;
            M_valE = m_valE = M_valA = m_valM = 0;
            W_valE = W_valM = 0;
            m_dmem_error = false;
            for (int i = 0; i < 16; i++)
                RegisterValue[i] = 0;
            CLOCK = 0;
            PenaltyCnt = 0;
            F_predPC = 0;
            F_predPC_s = F_predPC.ToString("X16");
            F_stall =  D_stall =  E_stall =  W_stall =  M_stall =  false;
            ZF = SF = OF = e_setCC = e_Cnd = false;
            F_bubble = D_bubble = E_bubble = W_bubble = M_bubble = true;
            GUIUpdate();
        }
        private void MemoryViewSetup()
        {
            MemoryList_s.Clear();
            long sz = MaxAddr/4;
            string tmp;
            for(int i = 0; i < sz; i++)
            {
                 tmp = ": ";
                for(int j = 0; j < 4; j++)
                {
                    tmp += (Convert.ToInt32(MemoryBlock[i * 4 + j])).ToString("X2");
                }
                int hd = i * 4;
                MemoryList_s.Add("0x" +hd.ToString("X")+ tmp);
            }
            WorkCompletedForMemory();
        }
        private bool MyIsDigit(char x)
        {
            return (x >= '0' && x <= '9') || (x >= 'A' && x <= 'F') || (x >= 'a' && x <= 'f');
        }
        private void StopPipe()
        {
            IsPause = true;
            this.PlayButtom.Icon = new SymbolIcon((Symbol)57602);
            this.PlayButtom.Label = "Play";
            Timer.Stop();
            this.DocsPageNavi.IsEnabled = true;
            F_predPC_s = F_predPC.ToString("X16");
            this.TestOutput.Text += "Break at " + F_predPC_s + "\r\n";
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
          
        }
        private void MemoryViewer_RefreshRequested(RefreshContainer sender, RefreshRequestedEventArgs args)
        {
            //Do some work to show new Content! Once the work is done, call RefreshCompletionDeferral.Complete()
            this.RefreshCompletionDeferralForMemory = args.GetDeferral();

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
            ProgramStat = "AOK";
            f_stat = D_stat = "AOK"; f_icode = D_icode = 1; f_ifun = D_ifun = 0; f_rA = D_rA = "NONE"; f_rB = D_rB = "NONE";
            d_stat = E_stat = "AOK"; d_icode = E_icode = 1; d_ifun = E_ifun = 0; d_dstE = d_dstM = d_srcA = d_srcB = E_dstE = E_dstM = E_srcA = E_srcB = "NONE";
            e_stat = M_stat = "AOK"; e_icode = M_icode = 1; e_ifun = M_ifun = 0; e_Cnd = M_Cnd = false; e_dstE = e_dstM = M_dstE = M_dstM = "NONE";
            m_stat = W_stat = "AOK"; m_icode = W_icode = 1; m_ifun = W_ifun = 0; m_dstE = m_dstM = W_dstE = W_dstM = "NONE";
            f_PredictPC = 0;
            F_predPC = 0;
            F_predPC_s = F_predPC.ToString("X16");
            f_valP = f_valC = 0;
            D_valC = d_valC = D_valP = d_valA = d_valB;
            E_valA = e_valA = E_valB = E_valC = 0;
            M_valE = m_valE = M_valA = m_valM = 0;
            W_valE = W_valM = 0;
            m_dmem_error = false;
            for (int i = 0; i < 16; i++)
                RegisterValue[i] = 0;
            CLOCK = 0;
            PenaltyCnt = 0;
            F_predPC = 0;
            F_predPC_s = F_predPC.ToString("X16");
            F_stall = D_stall = E_stall = W_stall = M_stall = false;
            ZF = SF = OF = e_setCC = e_Cnd = false;
            F_bubble = D_bubble = E_bubble = W_bubble = M_bubble = true;
            this.EndSwitch.IsEnabled = true;
            SourceText = null;
            SourceList.Clear();
            RealSource = null;
            SourceIsLoaded = false;
            MemoryBlock = null;
            MemoryList_s.Clear();
            WorkCompletedForSource();
            WorkCompletedForMemory();
            CLOCK = 0;
            IsPause = true;
            Preset();
            this.TestOutput.Text = "Reset\r\n";
            GUIUpdate();
        }
        private void PlayButtom_Click(object sender, RoutedEventArgs e)
        {
            IsPause = !IsPause;
            if (!IsPause) { 
            this.PlayButtom.Icon = new SymbolIcon((Symbol)57603);
                this.PlayButtom.Label = "Pause";
                Timer.Start();
                this.DocsPageNavi.IsEnabled = false;
                this.AboutPageNavi.IsEnabled = false;
                this.TestOutput.Text += "Play\r\n";
            }
            else
            {
                this.PlayButtom.Icon = new SymbolIcon((Symbol)57602);
                this.PlayButtom.Label = "Play";
                Timer.Stop();
                this.DocsPageNavi.IsEnabled = true;
                this.AboutPageNavi.IsEnabled = true;
                this.TestOutput.Text += "Pause\r\n";

            } 
        }
        private void SpeedSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            this.Timer.Interval = new TimeSpan(0, 0, 0, 0, (int)(1000 / SpeedSlider.Value));
        }
        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (ProgramStat == "AOK" && SourceIsLoaded) PipelineWork();
        }
        private void PreviousBottom_Click(object sender, RoutedEventArgs e)
        {
            if (CLOCK == 0) return;  
            long t = CLOCK - 1;
            SourceInit();
            UndoFlag = true;
            for(int i = 0; i < t; i++)
            {
                PipelineWork();
            }
            UndoFlag = false;
            GUIUpdate();
        }
        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            this.EndSwitch.IsEnabled = true;
            this.TestOutput.Text = "Refresh\r\n";
            if (CLOCK == 0) return;
            SourceInit();    
        }
        private void RegisterChange()
        {
            ToggleButton[] registers = new ToggleButton[15] { Hrax, Hrcx, Hrdx, Hrbx, Hrsp, Hrbp, Hrsi, Hrdi, Hr8, Hr9, Hr10, Hr11, Hr12, Hr13, Hr14 };
            for (int i = 0; i < 15; i++) registers[i].IsChecked = false;
            if(W_dstE!="NONE")
            registers[(int)RegisterHash[W_dstE]].IsChecked=true;
            if (W_dstM != "NONE")
                registers[(int)RegisterHash[W_dstM]].IsChecked = true;
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
        private void EndSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (this.EndSwitch.IsOn) Endflag = true;
            else Endflag = false;
        }
        private void ThemeSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            FrameworkElement window = (FrameworkElement)Window.Current.Content;
            if (this.ThemeSwitch.IsOn)
            {
                AppSettings.Theme = AppSettings.DEFAULTTHEME;
                window.RequestedTheme = AppSettings.DEFAULTTHEME;
            }
            else
            {
                AppSettings.Theme = AppSettings.NONDEFLTHEME;
                window.RequestedTheme = AppSettings.NONDEFLTHEME;
            }
        }
        private void RadixSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (this.RadixSwitch.IsOn)
            {
                XorD = "X16";
                this.HTOD.Content = "HEX";
            }
            else
            {
                XorD = "";
                this.HTOD.Content = "DEC";
            }
            GUIUpdate();
        }
        private void DelBP_Click(object sender, RoutedEventArgs e)
        {
            if (this.SBPway.SelectedIndex==0)
            {
                try { 
                    long target = Convert.ToInt64(this.BreakpointSetBox.Text,16);
                    if (BreakPoints.Contains(target))
                    {
                        BreakPoints.Remove(target);
                        int len = SourceList[Convert.ToInt32(LineHash[target])].Length;
                        SourceList[Convert.ToInt32(LineHash[target])] = SourceList[Convert.ToInt32(LineHash[target])].Substring(0, len - 11);
                        this.TestOutput.Text += "Delete Breakpoint at 0x" + target.ToString("X16") + "\r\n";
                        WorkCompletedForSource();
                    }
                }
                catch (FormatException){}
                catch (OverflowException){}
                finally { }
            }
            else if (this.SBPway.SelectedIndex == 1)
            {
                if (SourceListView.SelectedIndex != -1)
                {
                    long target = SourceListView.SelectedIndex;
                    if (!AddrHash.Contains(target)) return;
                    target = (long)AddrHash[target];
                    if (BreakPoints.Contains(target))
                    {
                        BreakPoints.Remove(target);
                        int len = SourceList[Convert.ToInt32(LineHash[target])].Length;
                        SourceList[Convert.ToInt32(LineHash[target])] = SourceList[Convert.ToInt32(LineHash[target])].Substring(0, len - 11);
                        this.TestOutput.Text += "Delete Breakpoint at 0x" + target.ToString("X16") + "\r\n";
                        WorkCompletedForSource();
                    }
                }
            }
            else if (this.SBPway.SelectedIndex == 2)
            {
                try
                {
                    long target = Convert.ToInt64(this.BreakpointSetBox.Text);
                    if (!AddrHash.Contains(target)) return;
                    target = (long)AddrHash[target];
                    if (BreakPoints.Contains(target))
                    {
                        BreakPoints.Remove(target);
                        int len = SourceList[Convert.ToInt32(LineHash[target])].Length;
                        SourceList[Convert.ToInt32(LineHash[target])]= SourceList[Convert.ToInt32(LineHash[target])].Substring(0,len-11);
                        this.TestOutput.Text += "Delete Breakpoint at 0x" + target.ToString("X16") + "\r\n";
                        WorkCompletedForSource();
                    }
                }
                catch (FormatException) { }
                catch (OverflowException) { }
                finally { }
            }
        }
        private void SetBP_Click(object sender, RoutedEventArgs e)
        {
            if (this.SBPway.SelectedIndex == 0)
            {
                try
                {
                    long target = Convert.ToInt64(this.BreakpointSetBox.Text,16);
                    if (!BreakPoints.Contains(target))
                    {
                        BreakPoints.Add(target);
                        SourceList[Convert.ToInt32(LineHash[target])] += "--BreakHere";
                        this.TestOutput.Text += "Set Breakpoint at 0x" + target.ToString("X16") + "\r\n";
                        WorkCompletedForSource();
                    }
                }
                catch (FormatException) { }
                catch (OverflowException) { }
                finally { }
            }
            else if (this.SBPway.SelectedIndex== 1)
            {
                if (SourceListView.SelectedIndex != -1)
                {
                    long target = SourceListView.SelectedIndex;
                    if (!AddrHash.ContainsKey(target)) return;
                    target = (long)AddrHash[target];
                    if (!BreakPoints.Contains(target))
                    {
                        BreakPoints.Add(target);
                        SourceList[Convert.ToInt32(LineHash[target])] += "--BreakHere";
                        this.TestOutput.Text += "Set Breakpoint at 0x" + target.ToString("X16") + "\r\n";
                        WorkCompletedForSource();
                    }
                }
            }
            else if (this.SBPway.SelectedIndex== 2)
            {
                try
                {
                    long target = Convert.ToInt64(this.BreakpointSetBox.Text);
                    if (!AddrHash.ContainsKey((object)target)) return;
                    target = (long)AddrHash[target];
                    if (!BreakPoints.Contains(target))
                    {
                        BreakPoints.Add(target);
                        SourceList[Convert.ToInt32(LineHash[target])] += "--BreakHere";
                        this.TestOutput.Text += "Set Breakpoint at 0x" + target.ToString("X16") + "\r\n";
                        WorkCompletedForSource();
                    }
                }
                catch (FormatException) { }
                catch (OverflowException) { }
                finally { }
            }
        }
        private void MusicSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (this.MusicSwitch.IsOn)
            {
                MusicPlayer.Play();
            }
            else
            {
                MusicPlayer.Pause();
            }
        }
    }
    class AppSettings
    {
        public const ElementTheme DEFAULTTHEME = ElementTheme.Light;
        public const ElementTheme NONDEFLTHEME = ElementTheme.Dark;

        const string KEY_THEME = "appColourMode";
        static ApplicationDataContainer LOCALSETTINGS = ApplicationData.Current.LocalSettings;
        /// <summary>
        /// Gets or sets the current app colour setting from memory (light or dark mode).
        /// </summary>
        public static ElementTheme Theme
        {
            get
            {
                // Never set: default theme
                if (LOCALSETTINGS.Values[KEY_THEME] == null)
                {
                    LOCALSETTINGS.Values[KEY_THEME] = (int)DEFAULTTHEME;
                    return DEFAULTTHEME;
                }
                // Previously set to default theme
                else if ((int)LOCALSETTINGS.Values[KEY_THEME] == (int)DEFAULTTHEME)
                    return DEFAULTTHEME;
                // Previously set to non-default theme
                else
                    return NONDEFLTHEME;
            }
            set
            {
                // Error check
                if (value == ElementTheme.Default)
                    throw new System.Exception("Only set the theme to light or dark mode!");
                // Never set
                else if (LOCALSETTINGS.Values[KEY_THEME] == null)
                    LOCALSETTINGS.Values[KEY_THEME] = (int)value;
                // No change
                else if ((int)value == (int)LOCALSETTINGS.Values[KEY_THEME])
                    return;
                // Change
                else
                    LOCALSETTINGS.Values[KEY_THEME] = (int)value;
            }
        }
    }
}