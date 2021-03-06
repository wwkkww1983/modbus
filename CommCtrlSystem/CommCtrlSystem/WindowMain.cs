﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO.Ports;
using System.IO;
using Newtonsoft.Json;
using System.Drawing.Printing;
using Modbus.Device;
using Modbus.IO;
using Modbus.Utility;
using Modbus.Data;
using System.Text.RegularExpressions;
using System.Threading;
using System.Data.OleDb;
using System.Net;
using System.Net.Sockets;

namespace CommCtrlSystem
{
    public partial class WindowMain : UserControl
    {
        public Thread updateDataThread;
        public bool m_updateDataFlg = false;
        ModbusRegisters modbusRegs;
        public delegate void UpdateMainUIInvoke(ModbusRegisters modbusRegs);
        private static readonly object locker = new object();

        private const byte SLAVEID = 1;
        private const ushort STARTADDRESS = 0;
        private const ushort REGNUM = 19;

        // Register define
        private const int TEMP_OIL0 = 0;
        private const int TEMP_OIL1 = 1;
        private const int TEMP_BATH0 = 2;
        private const int TEMP_BATH1 = 3;
        private const int COLDFILTERPOINT0 = 4;
        private const int COLDFILTERPOINT1 = 5;
        private const int PID_RESULT0 = 6;
        private const int PID_RESULT1 = 7;

        private const int STATE_WORK = 8;
        private const int RUNTIME_M = 9;
        private const int RUNTIME_S = 10;
        private const int TIME_SUCK = 11;
        private const int TIME_DROP = 12;
        private const int ALARM = 13;
        private const int STATE_SUCKTUBE = 14;
        private const int CHECKFINISH = 15;
        private const int UP_FLAG = 16;
        private const int DOWN_FLAG = 17;
        private const int ALLSTATE = 18;

        private const int ROOMNUM_LEFT = 0;
        private const int ROOMNUM_RIGHT = 1;

        private short coldfilterpoint0 = 0;
        private short coldfilterpoint1 = 0;

        private bool l_report_flg = false;
        private bool r_report_flg = false;

        WindowRealtimeData wrd1;
        WindowRealtimeData wrd2;
        
        public WindowMain()
        {
            InitializeComponent();
            InitializeRegs();
            string dbfile = System.IO.Path.Combine(Application.StartupPath, "CCSData.mdb");
            AccessHelper.initAccessHelper(dbfile);
            getConfig();

            wrd1 = new WindowRealtimeData();
            wrd2 = new WindowRealtimeData();


            WindowManager.GetInstance().wrd1 = wrd1;
            WindowManager.GetInstance().wrd2 = wrd2;

            this.textBoxDevNo0.Leave += new System.EventHandler(this.textBox_Leave);
            this.textBoxName0.Leave += new System.EventHandler(this.textBox_Leave);
            this.textBoxNo0.Leave += new System.EventHandler(this.textBox_Leave);
            this.textBoxOp0.Leave += new System.EventHandler(this.textBox_Leave);
            this.textBoxDevNo1.Leave += new System.EventHandler(this.textBox_Leave);
            this.textBoxName1.Leave += new System.EventHandler(this.textBox_Leave);
            this.textBoxNo1.Leave += new System.EventHandler(this.textBox_Leave);
            this.textBoxOp1.Leave += new System.EventHandler(this.textBox_Leave);
        }

        void InitializeRegs()
        {
            modbusRegs = new ModbusRegisters(SLAVEID, STARTADDRESS, REGNUM);
            modbusRegs.stReg[CHECKFINISH].addStringMap(0, "");
            modbusRegs.stReg[CHECKFINISH].addStringMap(1, "冷滤点完成");
            modbusRegs.stReg[CHECKFINISH].addStringMap(2, "<51℃，未阻塞");
            modbusRegs.stReg[CHECKFINISH].addStringMap(3, "首次吸引超过60秒，放弃");
            modbusRegs.stReg[CHECKFINISH].addStringMap(4, "用户温度下限未阻塞");

            modbusRegs.stReg[STATE_WORK].addStringMap(0, "停止");
            modbusRegs.stReg[STATE_WORK].addStringMap(1, "加热");
            modbusRegs.stReg[STATE_WORK].addStringMap(2, "制冷");
            modbusRegs.stReg[STATE_WORK].addStringMap(3, "完成");
            modbusRegs.stReg[STATE_WORK].addStringMap(4, "故障");

            modbusRegs.stReg[ALARM].addStringMap(0, "无故障");
            modbusRegs.stReg[ALARM].addStringMap(1, "加热器故障");
            modbusRegs.stReg[ALARM].addStringMap(2, "制冷故障");
            modbusRegs.stReg[ALARM].addStringMap(3, "光电检测故障");
            modbusRegs.stReg[ALARM].addStringMap(4, "电磁阀故障");
            modbusRegs.stReg[ALARM].addStringMap(5, "超温故障");
            modbusRegs.stReg[ALARM].addStringMap(6, "制冷超时故障");
            modbusRegs.stReg[ALARM].addStringMap(7, "加热超时故障");
            modbusRegs.stReg[ALARM].addStringMap(8, "温度传感器故障");

            modbusRegs.stReg[STATE_SUCKTUBE].addStringMap(0, "无动作");
            modbusRegs.stReg[STATE_SUCKTUBE].addStringMap(1, "吸引");
            modbusRegs.stReg[STATE_SUCKTUBE].addStringMap(2, "释放");

            modbusRegs.stReg[UP_FLAG].addStringMap(0, "OFF");
            modbusRegs.stReg[UP_FLAG].addStringMap(1, "ON");

            modbusRegs.stReg[DOWN_FLAG].addStringMap(0, "OFF");
            modbusRegs.stReg[DOWN_FLAG].addStringMap(1, "ON");

            modbusRegs.stReg[ALLSTATE].addStringMap(0, "停止");
            modbusRegs.stReg[ALLSTATE].addStringMap(1, "启动");
        }

        private void WindowMain_Load(object sender, EventArgs e)
        {

        }

        private void btnPrint_Click(object sender, EventArgs e)
        {
            PaperSize p = null;
            this.printDialog1.Document = printDocument1;
            DialogResult dr = this.printDialog1.ShowDialog();
            if (dr == DialogResult.OK)
            {
                foreach (PaperSize ps in printDocument1.PrinterSettings.PaperSizes)
                {
                    if (ps.PaperName.Equals("A4"))
                        p = ps;
                }

                this.printDocument1.DefaultPageSettings.PaperSize = p;
                this.printDocument1.PrintPage += new PrintPageEventHandler(this.MyPrintDocument_PrintPage);

                printPreviewDialog1.Document = printDocument1;

                printPreviewDialog1.ShowDialog();
            }
        }

        private void MyPrintDocument_PrintPage(object sender, System.Drawing.Printing.PrintPageEventArgs e)
        {
            /*如果需要改变自己 可以在new Font(new FontFamily("黑体"),11）中的“黑体”改成自己要的字体就行了，黑体 后面的数字代表字体的大小
             System.Drawing.Brushes.Blue , 170, 10 中的 System.Drawing.Brushes.Blue 为颜色，后面的为输出的位置 */
            e.Graphics.DrawString("滤点分析仪结果报告", new Font(new FontFamily("黑体"), 11), System.Drawing.Brushes.Black, 170, 10);

            e.Graphics.DrawLine(Pens.Black, 8, 30, 480, 30);
            e.Graphics.DrawString("日期", new Font(new FontFamily("黑体"), 8), System.Drawing.Brushes.Black, 9, 35);
            e.Graphics.DrawString("测定结果", new Font(new FontFamily("黑体"), 8), System.Drawing.Brushes.Black, 160, 35);
            e.Graphics.DrawString("试样编号", new Font(new FontFamily("黑体"), 8), System.Drawing.Brushes.Black, 260, 35);
            e.Graphics.DrawString("操作员", new Font(new FontFamily("黑体"), 8), System.Drawing.Brushes.Black, 330, 35);
            e.Graphics.DrawString("运行时间", new Font(new FontFamily("黑体"), 8), System.Drawing.Brushes.Black, 400, 35);
            e.Graphics.DrawLine(Pens.Black, 8, 50, 480, 50);
            //产品信息
            e.Graphics.DrawString(DateTime.Now.ToString(), new Font(new FontFamily("黑体"), 8), System.Drawing.Brushes.Black, 9, 55);
            e.Graphics.DrawString(coldfilterpoint0.ToString(), new Font(new FontFamily("黑体"), 8), System.Drawing.Brushes.Black, 160, 55);
            e.Graphics.DrawString(textBoxNo0.Text, new Font(new FontFamily("黑体"), 8), System.Drawing.Brushes.Black, 260, 55);
            e.Graphics.DrawString(textBoxOp0.Text, new Font(new FontFamily("黑体"), 8), System.Drawing.Brushes.Black, 330, 55);
            e.Graphics.DrawString(textBoxTime0.Text, new Font(new FontFamily("黑体"), 8), System.Drawing.Brushes.Black, 400, 55);

            e.Graphics.DrawString(DateTime.Now.ToString(), new Font(new FontFamily("黑体"), 8), System.Drawing.Brushes.Black, 9, 75);
            e.Graphics.DrawString(coldfilterpoint1.ToString(), new Font(new FontFamily("黑体"), 8), System.Drawing.Brushes.Black, 160, 75);
            e.Graphics.DrawString(textBoxNo1.Text, new Font(new FontFamily("黑体"), 8), System.Drawing.Brushes.Black, 260, 75);
            e.Graphics.DrawString(textBoxOp1.Text, new Font(new FontFamily("黑体"), 8), System.Drawing.Brushes.Black, 330, 75);
            e.Graphics.DrawString(textBoxTime1.Text, new Font(new FontFamily("黑体"), 8), System.Drawing.Brushes.Black, 400, 75);

            e.Graphics.DrawLine(Pens.Black, 8, 200, 480, 200);
        }
        private void btnSysCfg_Click(object sender, EventArgs e)
        {
            FormSysCfg fsc = new FormSysCfg();
            fsc.ShowDialog();
            getConfig();
        }

        private void saveInfo()
        {
            try
            {
                Configure cfg = null;
                string cfgfile = System.IO.Path.Combine(Application.StartupPath, "cfg.json");
                if (File.Exists(cfgfile))
                {
                    cfg = JsonConvert.DeserializeObject<Configure>(File.ReadAllText(cfgfile));
                }

                if (cfg == null)
                {
                    cfg = new Configure();
                }

                cfg.device_id = textBoxDevNo0.Text;
                cfg.sample_name = textBoxName0.Text;
                cfg.sample_id = textBoxNo0.Text;
                cfg.operator_name = textBoxOp0.Text;

                cfg.device_id2 = textBoxDevNo1.Text;
                cfg.sample_name2 = textBoxName1.Text;
                cfg.sample_id2 = textBoxNo1.Text;
                cfg.operator_name2 = textBoxOp1.Text;

                File.WriteAllText(cfgfile, JsonConvert.SerializeObject(cfg));
            }
            catch (Exception ex)
            {
                LogClass.GetInstance().WriteExceptionLog(ex);
                //MessageBox.Show(ex.ToString(), "Error - No Ports available", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void getConfig()
        {
            try
            {
                Configure cfg = null;
                string cfgfile = System.IO.Path.Combine(Application.StartupPath, "cfg.json");
                if (File.Exists(cfgfile))
                {
                    cfg = JsonConvert.DeserializeObject<Configure>(File.ReadAllText(cfgfile));
                    if (cfg != null)
                    {
                        if (inputCommPortSingleton.GetInstance().checkSerialPort(cfg.InputSerialPortName))
                        {
                            textBoxCom1.Text = cfg.InputSerialPortName;
                        }
                        else
                        {
                            textBoxCom1.Text = "";
                        }

                        if (inputCommPortSingleton.GetInstance().checkSerialPort(cfg.OutputSerialPortName))
                        {
                            textBoxCom2.Text = cfg.OutputSerialPortName;
                        }
                        else
                        {
                            textBoxCom2.Text = "";
                        }    

                        textBoxServerIP.Text = cfg.ServerIp;
                        textBoxServerPort.Text = cfg.ServerPort.ToString();

                        textBoxDevNo0.Text = cfg.device_id;
                        textBoxName0.Text = cfg.sample_name;
                        textBoxNo0.Text = cfg.sample_id;
                        textBoxOp0.Text = cfg.operator_name;

                        textBoxDevNo1.Text = cfg.device_id2;
                        textBoxName1.Text = cfg.sample_name2;
                        textBoxNo1.Text = cfg.sample_id2;
                        textBoxOp1.Text = cfg.operator_name2;

                        if (cfg.bGetDataOnload && inputCommPortSingleton.GetInstance().checkSerialPort(cfg.InputSerialPortName))
                        {
                            //startUpdateRegs();

                            l_report_flg = false;
                            r_report_flg = false;

                            if (false == inputCommPortSingleton.GetInstance().initComm() || false == inputCommPortSingleton.GetInstance().openComm())
                            {
                                btnStop_Click(null, null);
                            }
                            else
                            {
                                startUpdateRegs();
                            }
                        }
                        else
                        {
                            btnStop.Enabled = false;
                            btnRTData1.Enabled = false;
                            btnRTData2.Enabled = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogClass.GetInstance().WriteExceptionLog(ex);
                //MessageBox.Show(ex.ToString(), "Error - No Ports available", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }



        public void DoUpdateRegs()
        {
            //Thread.Sleep(1000);
            //inputCommPortSingleton.GetInstance().initComm();
            //if (false == inputCommPortSingleton.GetInstance().openComm())
            //{
            //    btnStop_Click(null, null);
            //}
            //Random random = new Random();
            while (m_updateDataFlg)  
            {
                try
                {
                    int ret = inputCommPortSingleton.GetInstance().readRegister(ref modbusRegs);
                    if (ret != inputCommPortSingleton.RET_OK)
                    {
                        if (ret == inputCommPortSingleton.RET_TIMEOUT)
                        {
                            continue;
                        }
                        else
                        {
                            // set label communication error
                        }
                        break;
                    }
                    UpdateMainUIInvoke umi = new UpdateMainUIInvoke(UpdateUIData);

                    while (!this.IsHandleCreated)
                    {
                        Thread.Sleep(100);
                    }
                    BeginInvoke(umi, modbusRegs);
                    if (modbusRegs.stReg[CHECKFINISH].getHighReg() != 0 && !l_report_flg)
                    {
                        byte[] imageLeft = WindowManager.GetInstance().wrd1.getImageData();
                        //byte[] imageRight = WindowManager.GetInstance().wrd2.getImageData();

                        //Left room
                        string sql1 = "insert into ModbusResultTable(room, TestDate, TestTime, TestResult, TestNo, Operator, [TestImage]) values('left','" + DateTime.Now + "', '" + textBoxTime0.Text + "', '" + coldfilterpoint0.ToString() + "', '" + textBoxNo0.Text + "', '" + textBoxOp0.Text + "', @imageLeft)";

                        OleDbParameter[] pars = new OleDbParameter[1];

                        OleDbParameter p = new OleDbParameter("@imageLeft", OleDbType.VarBinary, imageLeft.Length);
                        p.Value = imageLeft;

                        pars[0] = p;
                        int i = AccessHelper.ExecuteNonQuery(AccessHelper.ConnString, sql1, pars);

                        //Right room
                        //string sql2 = "insert into ModbusResultTable(room, TestDate, TestTime, TestResult, TestNo, Operator, [TestImage]) values('right','" + DateTime.Now + "', '" + textBoxTime2.Text + "','" + coldfilterpoint1.ToString() + "', '" + textBoxNo2.Text + "', '" + textBoxOp2.Text + "', @imageRight)";
                        //OleDbParameter[] pars2 = new OleDbParameter[1];

                        // OleDbParameter p2 = new OleDbParameter("@imageRight", OleDbType.VarBinary, imageRight.Length);
                        //p2.Value = imageRight;

                        //pars2[0] = p2;
                        //i = AccessHelper.ExecuteNonQuery(AccessHelper.ConnString, sql2, pars2);
                        //stopUpdateRegs();
                        saveXMLFile(ROOMNUM_LEFT);
                        saveExcelFile(ROOMNUM_LEFT);
                        byte[] serverData = getAsciiData(coldfilterpoint0.ToString(), textBoxTime0.Text, textBoxNo0.Text, textBoxName0.Text, textBoxDevNo0.Text, textBoxOp0.Text);
                        transResultToServer(serverData);
                        //getAsciiData("", "", "", "", "", "");
                        l_report_flg = true;
                    }

                    if (modbusRegs.stReg[CHECKFINISH].getLowReg() != 0 && !r_report_flg)
                    {
                        //byte[] imageLeft = WindowManager.GetInstance().wrd1.getImageData();
                        byte[] imageRight = WindowManager.GetInstance().wrd2.getImageData();

                        //Left room
                        //string sql1 = "insert into ModbusResultTable(room, TestDate, TestTime, TestResult, TestNo, Operator, [TestImage]) values('left','" + DateTime.Now + "', '" + textBoxTime1.Text + "', '" + coldfilterpoint0.ToString() + "', '" + textBoxNo1.Text + "', '" + textBoxOp1.Text + "', @imageLeft)";

                        //OleDbParameter[] pars = new OleDbParameter[1];

                        //OleDbParameter p = new OleDbParameter("@imageLeft", OleDbType.VarBinary, imageLeft.Length);
                        //p.Value = imageLeft;

                        //pars[0] = p;
                        //int i = AccessHelper.ExecuteNonQuery(AccessHelper.ConnString, sql1, pars);

                        //Right room
                        string sql2 = "insert into ModbusResultTable(room, TestDate, TestTime, TestResult, TestNo, Operator, [TestImage]) values('right','" + DateTime.Now + "', '" + textBoxTime1.Text + "','" + coldfilterpoint1.ToString() + "', '" + textBoxNo1.Text + "', '" + textBoxOp1.Text + "', @imageRight)";
                        OleDbParameter[] pars2 = new OleDbParameter[1];

                        OleDbParameter p2 = new OleDbParameter("@imageRight", OleDbType.VarBinary, imageRight.Length);
                        p2.Value = imageRight;

                        pars2[0] = p2;
                        int i = AccessHelper.ExecuteNonQuery(AccessHelper.ConnString, sql2, pars2);
                        //stopUpdateRegs();
                        saveXMLFile(ROOMNUM_RIGHT);
                        saveExcelFile(ROOMNUM_RIGHT);
                        byte[] serverData = getAsciiData(coldfilterpoint1.ToString(), textBoxTime1.Text, textBoxNo1.Text, textBoxName1.Text, textBoxDevNo1.Text, textBoxOp1.Text);
                        transResultToServer(serverData);
                        r_report_flg = true;
                    }
                    Thread.Sleep(1000);
                }
                catch (Exception ex)
                {
                    LogClass.GetInstance().WriteExceptionLog(ex);
                    //MessageBox.Show(ex.ToString(), "Error - No Ports available", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                }
            }

            while (m_updateDataFlg)
            {
                try
                {
                    Thread.Sleep(1000);
                }
                catch (Exception ex)
                {
                    LogClass.GetInstance().WriteExceptionLog(ex);
                }
            }
            inputCommPortSingleton.GetInstance().closeComm();
        }

        public void UpdateUIData(ModbusRegisters reg)
        {
            textBoxRes0.Text = reg.stReg[CHECKFINISH].getHighRegString();
            textBoxRes1.Text = reg.stReg[CHECKFINISH].getLowRegString();

            textBox2.Text = reg.stReg[TEMP_OIL0].getFloatValue().ToString();
            textBox9.Text = reg.stReg[TEMP_OIL1].getFloatValue().ToString();

            textBox3.Text = reg.stReg[TEMP_BATH0].getShortValue().ToString();
            textBox10.Text = reg.stReg[TEMP_BATH1].getShortValue().ToString();

            textBox3.Text = reg.stReg[TEMP_BATH0].getShortValue().ToString();
            textBox10.Text = reg.stReg[TEMP_BATH1].getShortValue().ToString();

            textBox4.Text = reg.stReg[ALLSTATE].getHighRegString();
            textBox11.Text = reg.stReg[ALLSTATE].getLowRegString();

            int run_time_h0 = reg.stReg[RUNTIME_M].getHighReg() / 60;
            int run_time_h1 = reg.stReg[RUNTIME_M].getLowReg() / 60;
            int run_time_m0 = reg.stReg[RUNTIME_M].getHighReg() % 60;
            int run_time_m1 = reg.stReg[RUNTIME_M].getLowReg() % 60;
            int run_time_s0 = reg.stReg[RUNTIME_S].getHighReg();
            int run_time_s1 = reg.stReg[RUNTIME_S].getLowReg();
            textBoxTime0.Text = string.Format("{0:D2}:{1:D2}:{2:D2}", run_time_h0, run_time_m0, run_time_s0);
            textBoxTime1.Text = string.Format("{0:D2}:{1:D2}:{2:D2}", run_time_h1, run_time_m1, run_time_s1);

            coldfilterpoint0 = reg.stReg[COLDFILTERPOINT0].getShortValue();
            coldfilterpoint1 = reg.stReg[COLDFILTERPOINT1].getShortValue();

            WindowManager.GetInstance().wrd1.updateData(0, reg);
            WindowManager.GetInstance().wrd2.updateData(1, reg);

            //Random random = new Random();
            //WindowManager.GetInstance().wrd1.addPoint0(random.Next(0, 10), random.Next(30, 50));
            //WindowManager.GetInstance().wrd2.addPoint0(random.Next(90, 100), random.Next(10, 40));
        }

        public void startUpdateRegs()
        {
            btnStart.Enabled = false;
            btnStop.Enabled = true;
            btnRTData1.Enabled = true;
            btnRTData2.Enabled = true;

            lock (locker)
            {
                if (!m_updateDataFlg)
                {
                    m_updateDataFlg = true;
                    updateDataThread = new Thread(new ThreadStart(DoUpdateRegs));
                    updateDataThread.Start();
                }
            }
        }

        public void stopUpdateRegs()
        {
            lock (locker)
            {
                if (m_updateDataFlg)
                {
                    m_updateDataFlg = false;
                    LogClass.GetInstance().WriteLogFile("Join");
                    updateDataThread.Join();
                    LogClass.GetInstance().WriteLogFile("Joined");
                }
            }
            btnStart.Enabled = true;
            btnStop.Enabled = false;
            btnRTData1.Enabled = false;
            btnRTData2.Enabled = false;
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            //mRealtimeFlg = 1;
            l_report_flg = false;
            r_report_flg = false;

            //inputCommPortSingleton.GetInstance().initComm();
            if (false == inputCommPortSingleton.GetInstance().initComm() || false == inputCommPortSingleton.GetInstance().openComm())
            {
                btnStop_Click(null, null);

                if (textBoxCom1.Text == "")
                {
                    btnSysCfg_Click(null, null);
                }
            }
            else
            {
                startUpdateRegs();
            }
            //btnStart.Enabled = false;
            //btnStop.Enabled = true;
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            stopUpdateRegs();
            //btnStart.Enabled = true;
        }

        private bool checkAuth()
        {
            if (!WindowManager.GetInstance().checkAuth())
            {
                AuthForm authForm = new AuthForm();
                if (authForm.ShowDialog(this) == DialogResult.No)
                {
                    return false;
                }
                else
                {
                    if (!WindowManager.GetInstance().checkAuth())
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private void btnBaseSetting_Click(object sender, EventArgs e)
        {
            if (!checkAuth())
            {
                return;
            }

            GroupBox tgb =  WindowManager.GetInstance().gb;
            tgb.Controls.Clear();
            tgb.Controls.Add(WindowManager.GetInstance().wbs);
            WindowManager.GetInstance().wbs.update();
        }

        private void btnHistReport_Click(object sender, EventArgs e)
        {
            //if (!checkAuth())
            //{
            //    return;
            //}
            GroupBox tgb = WindowManager.GetInstance().gb;
            tgb.Controls.Clear();
            WindowManager.GetInstance().whr.RefreshData();
            tgb.Controls.Add(WindowManager.GetInstance().whr);
        }

        private void btnTempCorret_Click(object sender, EventArgs e)
        {
            if (!checkAuth())
            {
                return;
            }
            GroupBox tgb = WindowManager.GetInstance().gb;
            tgb.Controls.Clear();
            tgb.Controls.Add(WindowManager.GetInstance().wtc);
            WindowManager.GetInstance().wtc.update();
        }

        private void btnPIDSetting_Click(object sender, EventArgs e)
        {
            if (!checkAuth())
            {
                return;
            }
            GroupBox tgb = WindowManager.GetInstance().gb;
            tgb.Controls.Clear();
            tgb.Controls.Add(WindowManager.GetInstance().wps);
            WindowManager.GetInstance().wps.update();
        }

        private void btnManualTest_Click(object sender, EventArgs e)
        {
            if (!checkAuth())
            {
                return;
            }
            GroupBox tgb = WindowManager.GetInstance().gb;
            tgb.Controls.Clear();
            tgb.Controls.Add(WindowManager.GetInstance().wms);
        }

        private void btnRTData1_Click(object sender, EventArgs e)
        {
            GroupBox tgb = WindowManager.GetInstance().gb;
            tgb.Controls.Clear();
            tgb.Controls.Add(WindowManager.GetInstance().wrd1);
        }

        private void btnRTData2_Click(object sender, EventArgs e)
        {
            GroupBox tgb = WindowManager.GetInstance().gb;
            tgb.Controls.Clear();
            tgb.Controls.Add(WindowManager.GetInstance().wrd2);
        }

        private void createExcelFile(string FileName, int room)
        {
            //create  
            try
            {
                NPOI.HSSF.UserModel.HSSFWorkbook book = new NPOI.HSSF.UserModel.HSSFWorkbook();
                NPOI.SS.UserModel.ISheet sheet = book.CreateSheet("Result");
                NPOI.SS.UserModel.IRow row0 = sheet.CreateRow(0);
                int i = 0;

                row0.CreateCell(i++).SetCellValue("日期:");
                row0.CreateCell(i++).SetCellValue("仪器名称:");
                row0.CreateCell(i++).SetCellValue("仪器编号:");
                row0.CreateCell(i++).SetCellValue("实验室名称:");
                row0.CreateCell(i++).SetCellValue("油样名称:");
                row0.CreateCell(i++).SetCellValue("油样号:");
                row0.CreateCell(i++).SetCellValue("实验员:");
                row0.CreateCell(i++).SetCellValue("分析方法:");
                row0.CreateCell(i++).SetCellValue("初次冷浴温度1:");
                row0.CreateCell(i++).SetCellValue("初次观察温度1:");
                row0.CreateCell(i++).SetCellValue("冷浴温度1:");
                row0.CreateCell(i++).SetCellValue("冷滤点1:");
                row0.CreateCell(i++).SetCellValue("初次冷浴温度2:");
                row0.CreateCell(i++).SetCellValue("初次观察温度2:");
                row0.CreateCell(i++).SetCellValue("冷浴温度2:");
                row0.CreateCell(i++).SetCellValue("冷滤点2:");


                NPOI.SS.UserModel.IRow row1 = sheet.CreateRow(1);
                i = 0;
                row1.CreateCell(i++).SetCellValue(DateTime.Now.ToString());
                row1.CreateCell(i++).SetCellValue("0");
                row1.CreateCell(i++).SetCellValue("0");
                row1.CreateCell(i++).SetCellValue("0");
                row1.CreateCell(i++).SetCellValue("0");
                row1.CreateCell(i++).SetCellValue("0");
                row1.CreateCell(i++).SetCellValue("0");
                row1.CreateCell(i++).SetCellValue("0");
                row1.CreateCell(i++).SetCellValue("0");
                row1.CreateCell(i++).SetCellValue("0");
                row1.CreateCell(i++).SetCellValue("0");
                row1.CreateCell(i++).SetCellValue("0");
                row1.CreateCell(i++).SetCellValue("0");
                row1.CreateCell(i++).SetCellValue("0");
                row1.CreateCell(i++).SetCellValue("0");
                row1.CreateCell(i++).SetCellValue("0");

                for (int j = 0; j < i; j++)
                {
                    sheet.AutoSizeColumn(j);
                }

                using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
                {
                    book.Write(ms);
                    using (FileStream fs = new FileStream(FileName, FileMode.Create, FileAccess.Write))
                    {
                        byte[] data = ms.ToArray();
                        fs.Write(data, 0, data.Length);
                        fs.Flush();
                    }
                    book = null;
                }
            }
            catch (Exception ex)
            {
                LogClass.GetInstance().WriteExceptionLog(ex);
                //MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        /// <summary>  
        /// If the supplied excel File does not exist then Create it  
        /// </summary>  
        /// <param name="FileName"></param>  
        //private void createExcelFile(string FileName, int room)
        //{
        //    //create  
        //    try
        //    {
        //        NPOI.HSSF.UserModel.HSSFWorkbook book = new NPOI.HSSF.UserModel.HSSFWorkbook();
        //        NPOI.SS.UserModel.ISheet sheet = book.CreateSheet("Result");
        //        NPOI.SS.UserModel.IRow row0 = sheet.CreateRow(0);

        //        int i = 0;

        //        row0.CreateCell(i++).SetCellValue("试样编号:");
        //        row0.CreateCell(i++).SetCellValue("日期时间:");
        //        if (room == 0)
        //        {
        //            row0.CreateCell(i++).SetCellValue("左室测定结果:");
        //        }
        //        else
        //        {
        //            row0.CreateCell(i++).SetCellValue("右室测定结果:");
        //        }
        //        row0.CreateCell(i++).SetCellValue("运行时间:");
        //        row0.CreateCell(i++).SetCellValue("操作员:");

        //        NPOI.SS.UserModel.IRow row1 = sheet.CreateRow(1);
        //        i = 0;

        //        if (room == 0)
        //        {
        //            row1.CreateCell(i++).SetCellValue(textBoxNo0.Text);
        //            row1.CreateCell(i++).SetCellValue(DateTime.Now.ToString());
        //            row1.CreateCell(i++).SetCellValue(coldfilterpoint0.ToString());
        //            row1.CreateCell(i++).SetCellValue(textBoxTime0.Text);
        //            row1.CreateCell(i++).SetCellValue(textBoxOp0.Text);
        //        }
        //        else
        //        {
        //            row1.CreateCell(i++).SetCellValue(textBoxNo1.Text);
        //            row1.CreateCell(i++).SetCellValue(DateTime.Now.ToString());
        //            row1.CreateCell(i++).SetCellValue(coldfilterpoint1.ToString());
        //            row1.CreateCell(i++).SetCellValue(textBoxTime1.Text);
        //            row1.CreateCell(i++).SetCellValue(textBoxOp1.Text);
        //        }


        //        for (int j = 0; j < i; j++)
        //        {
        //            sheet.AutoSizeColumn(j);
        //        }

        //        using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
        //        {
        //            book.Write(ms);
        //            using (FileStream fs = new FileStream(FileName, FileMode.Create, FileAccess.Write))
        //            {
        //                byte[] data = ms.ToArray();
        //                fs.Write(data, 0, data.Length);
        //                fs.Flush();
        //            }
        //            book = null;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        LogClass.GetInstance().WriteExceptionLog(ex);
        //        //MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //    }
        //}

        private void btnSaveExcel_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();

            sfd.Filter = "Excel文件（*.xls）|*.xls|Excel文件（*.xlsx）|*.xlsx";

            sfd.FilterIndex = 1;

            sfd.RestoreDirectory = true;

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                string localFilePath = sfd.FileName.ToString(); //获得文件路径 
                string fileNameExt = localFilePath.Substring(localFilePath.LastIndexOf("\\") + 1);

                createExcelFile(localFilePath, 0);
            }
        }
        public void saveExcelFile(int roomNo)
        {
            DateTime now = DateTime.Now;
            int iNo = 1;
            string dir_path = "d:\\ExcelIN";

            Configure cfg = null;
            string cfgfile = System.IO.Path.Combine(Application.StartupPath, "cfg.json");
            if (File.Exists(cfgfile))
            {
                cfg = JsonConvert.DeserializeObject<Configure>(File.ReadAllText(cfgfile));
            }

            if (cfg != null && cfg.excel_dir != null)
            {
                dir_path = cfg.excel_dir;
            }

            if (!Directory.Exists(dir_path))
            {
                try
                {
                    Directory.CreateDirectory(dir_path);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Create dir " + dir_path + " failure!");
                }
            }

            string filename = String.Format(dir_path + "\\dlznyq_coldfilter_{0}-{1}.xls", now.ToString("yyyy-MM-dd"), iNo);
            while (File.Exists(filename))
            {
                iNo++;
                filename = String.Format(dir_path + "\\dlznyq_coldfilter_{0}-{1}.xls", now.ToString("yyyy-MM-dd"), iNo);
            }
            createExcelFile(filename, roomNo);
        }
        public void saveXMLFile(int roomNo)
        {
            DateTime now = DateTime.Now;
            int iNo = 1;

            string dir_path = "d:\\IN";

            Configure cfg = null;
            string cfgfile = System.IO.Path.Combine(Application.StartupPath, "cfg.json");
            if (File.Exists(cfgfile))
            {
                cfg = JsonConvert.DeserializeObject<Configure>(File.ReadAllText(cfgfile));
            }

            if (cfg != null && cfg.excel_dir != null)
            {
                dir_path = cfg.xml_dir;
            }

            if (!Directory.Exists(dir_path))
            {
                try
                {
                    Directory.CreateDirectory(dir_path);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Create dir " + dir_path + " failure!");
                }
            }

            string filename = String.Format(dir_path + "\\dlznyq_coldfilter_{0}-{1}.xml", now.ToString("yyyy-MM-dd"), iNo);
            while (File.Exists(filename))
            {
                iNo++;
                filename = String.Format(dir_path + "\\dlznyq_coldfilter_{0}-{1}.xml", now.ToString("yyyy-MM-dd"), iNo);
            }
            createXmlFile(filename, roomNo);
        }

        public bool createXmlFile(string filename, int roomNo)
        {
            try
            {
                Configure cfg = null;
                string cfgfile = System.IO.Path.Combine(Application.StartupPath, "cfg.json");
                if (File.Exists(cfgfile))
                {
                    cfg = JsonConvert.DeserializeObject<Configure>(File.ReadAllText(cfgfile));
                    if (cfg != null)
                    {
                        LimsDoc l;
                        l = new LimsDoc(cfg.username, cfg.userpassword, "system");

                        LimsDocEntity entity = l.createEntity("SAMPLE", "RESULT_ENTRY");
                        LimsDocEntity entity2 = l.createEntity("TEST", null);
                        LimsDocEntity entity3 = l.createEntity("RESULT", null);

                        LimsDocEntity entity1_time = l.createEntity("RESULT", null);
                        LimsDocEntity entity1_res = l.createEntity("RESULT", null);
                        LimsDocEntity entity2_time = l.createEntity("RESULT", null);
                        LimsDocEntity entity2_res = l.createEntity("RESULT", null);
                        LimsDocEntity entity3_time = l.createEntity("RESULT", null);
                        LimsDocEntity entity3_res = l.createEntity("RESULT", null);
                        LimsDocEntity entity4_time = l.createEntity("RESULT", null);
                        LimsDocEntity entity4_res = l.createEntity("RESULT", null);
                        entity2.addFields("ANALYSIS", "in", cfg.analysis);

                        if (roomNo == ROOMNUM_LEFT)
                        {
                            entity1_time.addFields("NAME", "in", "初次冷浴温度1");
                            entity1_time.addFields("TEXT", "in", "0");
                            entity2.addChild(entity1_time.getElement());

                            entity1_res.addFields("NAME", "in", "初次观察温度1");
                            entity1_res.addFields("TEXT", "in", "0");
                            entity2.addChild(entity1_res.getElement());

                            entity2_time.addFields("NAME", "in", "冷浴温度1");
                            entity2_time.addFields("TEXT", "in", "0");
                            entity2.addChild(entity2_time.getElement());

                            entity2_res.addFields("NAME", "in", "冷滤点1");
                            entity2_res.addFields("TEXT", "in", "0");
                            entity2.addChild(entity2_res.getElement());

                            entity3_time.addFields("NAME", "in", "初次冷浴温度2");
                            entity3_time.addFields("TEXT", "in", "0");
                            entity2.addChild(entity3_time.getElement());

                            entity3_res.addFields("NAME", "in", "初次观察温度2");
                            entity3_res.addFields("TEXT", "in", "0");
                            entity2.addChild(entity3_res.getElement());

                            entity4_time.addFields("NAME", "in", "冷浴温度2");
                            entity4_time.addFields("TEXT", "in", "0");
                            entity2.addChild(entity4_time.getElement());

                            entity4_res.addFields("NAME", "in", "冷滤点2");
                            entity4_res.addFields("TEXT", "in", "0");
                            entity2.addChild(entity4_res.getElement());
                        }
                        else if (roomNo == ROOMNUM_RIGHT)
                        {
                            entity1_time.addFields("NAME", "in", "初次冷浴温度1");
                            entity1_time.addFields("TEXT", "in", "1");
                            entity2.addChild(entity1_time.getElement());

                            entity1_res.addFields("NAME", "in", "初次观察温度1");
                            entity1_res.addFields("TEXT", "in", "1");
                            entity2.addChild(entity1_res.getElement());

                            entity2_time.addFields("NAME", "in", "冷浴温度1");
                            entity2_time.addFields("TEXT", "in", "1");
                            entity2.addChild(entity2_time.getElement());

                            entity2_res.addFields("NAME", "in", "冷滤点1");
                            entity2_res.addFields("TEXT", "in", "1");
                            entity2.addChild(entity2_res.getElement());

                            entity3_time.addFields("NAME", "in", "初次冷浴温度2");
                            entity3_time.addFields("TEXT", "in", "1");
                            entity2.addChild(entity3_time.getElement());

                            entity3_res.addFields("NAME", "in", "初次观察温度2");
                            entity3_res.addFields("TEXT", "in", "1");
                            entity2.addChild(entity3_res.getElement());

                            entity4_time.addFields("NAME", "in", "冷浴温度2");
                            entity4_time.addFields("TEXT", "in", "1");
                            entity2.addChild(entity4_time.getElement());

                            entity4_res.addFields("NAME", "in", "冷滤点2");
                            entity4_res.addFields("TEXT", "in", "1");
                            entity2.addChild(entity4_res.getElement());
                        }
                        else
                        {
                            return false;
                        }

                        entity.addChild(entity2.getElement());
                        l.getBody().addEntity(entity.getElement());

                        return l.createdoc(filename);
                    }
                }
            }
            catch (Exception ex)
            {
                LogClass.GetInstance().WriteExceptionLog(ex);
                //MessageBox.Show(ex.ToString(), "Error - No Ports available", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return false;

        }

        private bool fillByteArray(ref byte[] src, ref byte[] dst, int maxcplen, int dstofs, byte maskedbyte)
        {
            for (int i = 0; i < maxcplen; i++)
            {
                Buffer.SetByte(dst, i + dstofs, maskedbyte);
            }

            if (src.Length > maxcplen)
            {
                Buffer.BlockCopy(src, 0, dst, dstofs, maxcplen);
            }
            else
            {
                Buffer.BlockCopy(src, 0, dst, dstofs, src.Length);
            }
            return true;
        }

        private byte[] getAsciiData(string res, string time, string id, string name, string devid, string oper)
        {
            const int BLOCK_LEN = 16;
            const int NUM_OF_BLOCK = 6;
            byte[] data = new byte[BLOCK_LEN * NUM_OF_BLOCK];

            byte[] bRes = System.Text.Encoding.UTF8.GetBytes(res);
            byte[] bTime = System.Text.Encoding.UTF8.GetBytes(time);
            byte[] bID = System.Text.Encoding.UTF8.GetBytes(id);
            byte[] bName = System.Text.Encoding.UTF8.GetBytes(name);
            byte[] bDevid = System.Text.Encoding.UTF8.GetBytes(devid);
            byte[] bOper = System.Text.Encoding.UTF8.GetBytes(oper);

            fillByteArray(ref bRes, ref data, BLOCK_LEN, 0 * BLOCK_LEN, 0x00);
            fillByteArray(ref bTime, ref data, BLOCK_LEN, 1 * BLOCK_LEN, 0x00);
            fillByteArray(ref bID, ref data, BLOCK_LEN, 2 * BLOCK_LEN, 0x00);
            fillByteArray(ref bName, ref data, BLOCK_LEN, 3 * BLOCK_LEN, 0x00);
            fillByteArray(ref bDevid, ref data, BLOCK_LEN, 4 * BLOCK_LEN, 0x00);
            fillByteArray(ref bOper, ref data, BLOCK_LEN, 5 * BLOCK_LEN, 0x00);

            return data;
        }

        private decimal GetNumber(string str)
        {
            decimal result = 0;
            if (str != null && str != string.Empty)
            {
                // 正则表达式剔除非数字字符（不包含小数点.）
                str = Regex.Replace(str, @"[^\d.\d]", "");
                // 如果是数字，则转换为decimal类型
                if (Regex.IsMatch(str, @"^[+-]?\d*[.]?\d*$"))
                {
                    result = decimal.Parse(str);
                }
            }
            return result;
        }

        private bool transResultToServer(byte[] ascii)
        {
            try
            {
                Configure cfg = null;
                string cfgfile = System.IO.Path.Combine(Application.StartupPath, "cfg.json");
                if (File.Exists(cfgfile))
                {
                    cfg = JsonConvert.DeserializeObject<Configure>(File.ReadAllText(cfgfile));
                    if (cfg != null)
                    {
                        if (cfg.OutoutMethod == "串口")
                        {
                            if (cfg.OutputSerialPortName.Length == 0)
                            {
                                return true;
                            }

                            if (cfg.bGetDataOnload && inputCommPortSingleton.GetInstance().checkSerialPort(cfg.OutputSerialPortName))
                            {

                                using (SerialPort masterPort = new SerialPort(cfg.OutputSerialPortName))
                                {
                                    // configure serial ports

                                    masterPort.BaudRate = (int)GetNumber(cfg.OutputSerialPortBaud);
                                    masterPort.DataBits = (int)GetNumber(cfg.OutputSerialPortDataBit);
                                    if (cfg.OutputSerialPortParity == "None Parity")
                                    {
                                        masterPort.Parity = Parity.None;
                                    }
                                    else if (cfg.OutputSerialPortParity == "Odd Parity")
                                    {
                                        masterPort.Parity = Parity.Odd;
                                    }
                                    else
                                    {
                                        masterPort.Parity = Parity.Even;
                                    }

                                    if (cfg.OutputSerialPortStopBit == "1 Stop Bit")
                                    {
                                        masterPort.StopBits = StopBits.One;
                                    }
                                    else
                                    {
                                        masterPort.StopBits = StopBits.Two;
                                    }

                                    masterPort.ReadTimeout = 1000;
                                    masterPort.WriteTimeout = 1000;

                                    masterPort.Open();

                                    // create modbus master
                                    ModbusSerialMaster master = ModbusSerialMaster.CreateAscii(masterPort);

                                    master.Transport.Retries = 5;
                                    ushort startAddress = 0x08;
                                    ushort[] data = new ushort[ascii.Length / sizeof(short)];
                                    Buffer.BlockCopy(ascii, 0, data, 0, data.Length * sizeof(short));

                                    master.WriteMultipleRegisters(1, startAddress, data);
                                    // read five register values
                                    //ushort[] registers = master.ReadHoldingRegisters(slaveId, startAddress, numRegisters);
                                }
                            }
                        }
                        else
                        {

                            using (TcpClient client = new TcpClient(cfg.ServerIp, (int)GetNumber(cfg.ServerPort)))
                            {
                                ModbusSerialMaster master = ModbusSerialMaster.CreateAscii(client);

                                master.Transport.Retries = 5;
                                ushort startAddress = 0x08;
                                ushort[] data = new ushort[ascii.Length / sizeof(short)];
                                Buffer.BlockCopy(ascii, 0, data, 0, data.Length * sizeof(short));
                                client.SendTimeout = 1000;
                                client.ReceiveTimeout = 1000;
                                master.WriteMultipleRegisters(1, startAddress, data);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogClass.GetInstance().WriteExceptionLog(ex);
                //MessageBox.Show(ex.ToString(), "Error - No Ports available", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return true;
        }

        private void TextXmlBtn_Click(object sender, EventArgs e)
        {
            TestXMLForm testXMLForm = new TestXMLForm();
            testXMLForm.ShowDialog();
        }

        private void buttonTestLeftRoom_Click(object sender, EventArgs e)
        {
            byte[] serverData = getAsciiData(coldfilterpoint0.ToString(), textBoxTime0.Text, textBoxNo0.Text, textBoxName0.Text, textBoxDevNo0.Text, textBoxOp0.Text);
            transResultToServer(serverData);
        }

        private void buttonTestRightRoom_Click(object sender, EventArgs e)
        {
            byte[] serverData = getAsciiData(coldfilterpoint1.ToString(), textBoxTime1.Text, textBoxNo1.Text, textBoxName1.Text, textBoxDevNo1.Text, textBoxOp1.Text);
            transResultToServer(serverData);
        }

        private void buttonFloatTest_Click(object sender, EventArgs e)
        {
            FormFloatTest fft = new FormFloatTest();
            fft.ShowDialog();
        }

        private void textBox_Leave(object sender, EventArgs e)
        {
            saveInfo();
        }
    }
}
