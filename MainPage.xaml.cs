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
        bool IsPause = false, SourceIsLoaded = false;
        string[] RealSource;
        Windows.Storage.StorageFile SourceFile;
        string SourceText;
        DispatcherTimer Timer = new DispatcherTimer();
        string MachineCodeTest = "";



        //****************************************************************************
        const int RAX = 0, RCX = 1, RDX = 2, RBX = 3, RSP = 4, RBP = 5, RSI = 6, RDI = 7, R8 = 8, R9 = 9,
            R10 = 10, R11 = 11, R12 = 12, R13 = 13, R14 = 14, NONE = 15;

        long[] RegisterValue = new long[16];
        long CLOCK = 0;

        long F_predPC, f_SelectPC, f_pc, f_Split, f_icode, f_ifun, f_Align, f_NeedvalC, f_Needregids, f_valP, f_valC, f_PredictPC;
        string F_predPC_s, f_stat, f_rA, f_rB;
        bool f_imem_error;
        bool F_stall, F_bubble;


        string D_stat, d_stat, D_instr, d_instr, D_rA, d_srcA, D_rB, d_srcB, d_dstE, d_dstM;
        long D_valC, d_valC, D_valP, d_valP, d_icode, d_ifun, D_icode, D_ifun;
        long d_rvalA, d_rvalB, d_valA, d_valB;
        bool D_stall, D_bubble;

        string ProgramStat, INSMessage;

        string E_stat, e_stat, E_instr, e_instr, E_dstE, e_dstE, E_dstM, e_dstM, E_srcA, e_srcA, E_srcB, e_srcB;

        SolidColorBrush RegisterHighlight = new SolidColorBrush(Windows.UI.Color.FromArgb(1, 120, 133, 116));

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
        Hashtable LineHash = new Hashtable();

        char[] MemoryBlock = new char[1 << 20];
        char[] MemoryBlockCopy = new char[1 << 20];
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
            for(int i = 7; i >= 0; i--)
            {
                ret = (ret << 8) + (long)MemoryBlock[addr + i];
            }
            return ret;
        }
        private void Write8Bytes(long addr)
        {
            long tmp = DataEntry;
            for(int i=0;i<8;i++)
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
            this.MemoryListView.ItemsSource = MemoryList_s;
            this.SourceListView.ItemsSource = SourceList;
            Preset();
        }
        private void Timer_Tick(object sender, object e)
        { 
            //TODO
            if(!IsPause&&SourceIsLoaded&&ProgramStat=="AOK") PipelineWork();
            else
            {
                GUIUpdate();
                this.PlayButtom.Icon = new SymbolIcon((Symbol)57602);
                this.PlayButtom.Label = "Play";
                Timer.Stop();
                IsPause = true;
            }
        }

        private void PipelineWork()
        {
            
            CLOCK++;
            RegUpdate();
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
            F_predPC_s = F_predPC.ToString("X16");
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
            for(int i=0;i<15;i++)registers[i].Content= (RegisterValue[i]).ToString("X16");
            Bindings.Update();
            if (LineHash.Contains(F_predPC)){
                this.SourceListView.SelectedIndex = (int)LineHash[F_predPC];
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
            E_bubble = (E_icode == 7 && E_ifun > 0 && !e_Cnd) || ((E_icode == 5 || E_icode == 11) && (E_dstM == d_srcA || E_dstM == d_srcB)) ;

            M_stall = false;
            M_bubble = false;

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
            long ret=0;
            for(int i=0;i<8;i++){
                ret+=(long)((long)(MemoryBlock[index+i])<<(i*8));
            }
            return ret;            
        }
        private void Fetch()
        {
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
            if (f_pc < 0 || f_pc > InstrPointer)
            {
                f_imem_error = true;
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
                f_valP = f_pc + 2; f_PredictPC = f_valP;
            }
            else if(f_icode==3&&f_ifun==0 && ((MemoryBlock[f_pc + 1] >> 4) & 0xf)==0xf)
            {
                f_rA = RegisterCollection[(MemoryBlock[f_pc + 1] >> 4) & 0xf];
                f_rB = RegisterCollection[MemoryBlock[f_pc + 1] & 0xf];
                f_valC = Trans8Bytes(f_pc + 2);
                f_valP = f_pc + 10; f_PredictPC = f_valP;
            }
            else if((f_icode==4||f_icode==5)&&f_ifun==0)
            {
                f_rA = RegisterCollection[(MemoryBlock[f_pc + 1] >> 4) & 0xf];
                f_rB = RegisterCollection[MemoryBlock[f_pc + 1] & 0xf];
                f_valC = Trans8Bytes(f_pc + 2);
                f_valP = f_pc + 10; f_PredictPC = f_valP;
            }
            else if(f_icode==6&&f_ifun>=0&&f_ifun<=3)
            {
                f_rA = RegisterCollection[(MemoryBlock[f_pc + 1] >> 4) & 0xf];
                f_rB = RegisterCollection[MemoryBlock[f_pc + 1] & 0xf];
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
                f_valP = f_pc + 2; f_PredictPC = f_valP;
            }
            else
            {
                ProgramStat="INS";
                INSMessage="Unexpeted Instruction:"+f_icode.ToString("X16")+f_ifun.ToString("X16")+"\r\n";
                //TODO:ICODEERROR
            }

            if (f_icode == 0 && f_ifun == 0)
                f_stat = "HLT";
            else
                f_stat = "AOK";
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
            if(E_icode==6)
            {
                //TODO: unexpect control
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
            //TODO:stat Error
            m_icode = M_icode;
            m_valE = M_valE;
            m_valM = DataExit;
            m_dstE = M_dstE;
            m_dstM = M_dstM;
            m_ifun = M_ifun;
            m_stat = M_stat;
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
            //TODO
            SourceText = "NO INPUT YET";
            DataContext = this;
            Timer.Start();
            SourceIsLoaded = false;
            SourceList.Add("NO INPUT YET");
            WorkCompletedForMemory();
            WorkCompletedForSource();
            
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
             /*   for(int i = 1; i <= RealSource.Length; i++)
                {
                    SourceList.Add(i.ToString("D4")+"|    "+RealSource[i-1]);
                }*/
            }
            this.SourceListView.SelectedIndex = 0;
            WorkCompletedForSource();
            LoadInstructions();
        }
        private void LoadInstructions()
        {
            MachineCodeTest = "";
            MemoryBlockCopy = new char[1 << 20];
            int cnt = 0;
            LineHash.Clear();
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
                    }
                    else if (flag && MyIsDigit(RealSource[i][j]))
                    {
                        ValidLineFlag = true;
                        MachineCodeTest += RealSource[i][j];
                        MachineCodeTest += RealSource[i][j+1];
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
                    cnt++;
                }
            }
            this.TestOutput.Text = MachineCodeTest;
            SourceIsLoaded = true;
            SourceInit();
        }
        private void SourceInit()
        {
            MemoryBlock = (char[])MemoryBlockCopy.Clone();
            ProgramStat = "AOK"; INSMessage="";
            f_stat=D_stat = "AOK"; f_icode=D_icode = 1; f_ifun=D_ifun = 0; f_rA=D_rA = "NONE"; f_rB=D_rB = "NONE";
            d_stat=E_stat = "AOK"; d_icode=E_icode = 1; d_ifun=E_ifun = 0; d_dstE = d_dstM = d_srcA = d_srcB = E_dstE = E_dstM = E_srcA = E_srcB = "NONE";
            e_stat=M_stat = "AOK"; e_icode=M_icode = 1; e_ifun=M_ifun = 0; e_Cnd=M_Cnd = false;e_dstE = e_dstM =M_dstE =  M_dstM = "NONE";
            m_stat=W_stat = "AOK"; m_icode=W_icode = 1; m_ifun=W_ifun=w_ifun=0; m_dstE = m_dstM = W_dstE = W_dstM = "NONE";
            f_PredictPC = 0;

            f_valP= f_valC = 0;
            D_valC = d_valC = D_valP = d_valP = 0;
            E_valA = e_valA = E_valB = e_valB = E_valC = e_valC = 0;
            M_valE = m_valE = M_valA = m_valA = 0;
            W_valE = W_valM = 0;



            for (int i = 0; i < 16; i++)
                RegisterValue[i] = 0;
            CLOCK = 0;
            F_predPC = 0;
            F_predPC_s = F_predPC.ToString("X16");
            F_stall =  D_stall =  E_stall =  W_stall =  M_stall =  false;
            ZF = SF = OF = e_setCC = e_Cnd = false;
            F_bubble = D_bubble = E_bubble = W_bubble = M_bubble = true;
            GUIUpdate();
            //ProgramStat = "AOK";


            //TODO
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
                Timer.Start();
            }
            else
            {
                this.PlayButtom.Icon = new SymbolIcon((Symbol)57602);
                this.PlayButtom.Label = "Play";
                Timer.Stop();
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
            for(int i = 0; i < t; i++)
            {
                PipelineWork();
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
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
    }

}

