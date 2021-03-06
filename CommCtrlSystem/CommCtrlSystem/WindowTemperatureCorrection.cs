﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;

namespace CommCtrlSystem
{        
    public partial class WindowTemperatureCorrection : UserControl
    {
        private Thread updateDataThread;
        private Thread writeDataThread;
        private ModbusRegisters modbusRegs;
        private delegate void UpdateMainUIInvoke(ModbusRegisters modbusRegs);
        private delegate void WriteDataOKInvoke();
        private TextBox[] tbTemratureCorrection;
        private const byte SLAVEID = 1;
        private const ushort STARTADDRESS = 0x33;
        private const ushort REGNUM = 18;

        public WindowTemperatureCorrection()
        {
            InitializeComponent();
            InitializeRegs();
        }

        void InitializeRegs()
        {
            tbTemratureCorrection = new TextBox[18];
            modbusRegs = new ModbusRegisters(SLAVEID, STARTADDRESS, REGNUM);
            tbTemratureCorrection[0] = textBox1;
            tbTemratureCorrection[1] = textBox2;
            tbTemratureCorrection[2] = textBox3;
            tbTemratureCorrection[3] = textBox4;
            tbTemratureCorrection[4] = textBox5;
            tbTemratureCorrection[5] = textBox6;
            tbTemratureCorrection[6] = textBox7;
            tbTemratureCorrection[7] = textBox8;
            tbTemratureCorrection[8] = textBox9;
            tbTemratureCorrection[9] = textBox10;
            tbTemratureCorrection[10] = textBox11;
            tbTemratureCorrection[11] = textBox12;
            tbTemratureCorrection[12] = textBox13;
            tbTemratureCorrection[13] = textBox14;
            tbTemratureCorrection[14] = textBox15;
            tbTemratureCorrection[15] = textBox16;
            tbTemratureCorrection[16] = textBox17;
            tbTemratureCorrection[17] = textBox18;

            for (int i = 0; i < 17; i++)
            {
                tbTemratureCorrection[i].KeyPress += new KeyPressEventHandler(new CheckUserInput().CheckIsNumber);
            }
        }

        public void DoUpdateRegs()
        {
            inputCommPortSingleton.GetInstance().readRegister(ref modbusRegs);
            UpdateMainUIInvoke umi = new UpdateMainUIInvoke(UpdateUIData);
            if (this.IsHandleCreated)
            {
                BeginInvoke(umi, modbusRegs);
            }
        }

        public void UpdateUIData(ModbusRegisters reg)
        {
            for (int i = 0; i < reg.numRegisters; i++)
            {
                tbTemratureCorrection[i].Text = reg.stReg[i].getShortValue().ToString();
            }
            buttonWTRead.Enabled = true;
            buttonWTWrite.Enabled = true;
            buttonMain.Enabled = true;
        }

        private void buttonMain_Click(object sender, EventArgs e)
        {
            GroupBox tgb = WindowManager.GetInstance().gb;
            tgb.Controls.Clear();
            tgb.Controls.Add(WindowManager.GetInstance().wmain);
        }

        private void buttonWTRead_Click(object sender, EventArgs e)
        {
            //updateDataThread = new Thread(new ThreadStart(DoUpdateRegs));
            //updateDataThread.Start();
            buttonWTRead.Enabled = false;
            buttonWTWrite.Enabled = false;
            buttonMain.Enabled = false;
            updateDataThread = new Thread(new ThreadStart(DoUpdateRegs));
            updateDataThread.Start();
        }

        public void WriteDataOK()
        {
            buttonWTRead.Enabled = true;
            buttonWTWrite.Enabled = true;
            buttonMain.Enabled = true;
        }

        private void WriteThread()
        {
            try
            {
                for (int i = 0; i < modbusRegs.numRegisters; i++)
                {
                    modbusRegs.stReg[i].setValue(ushort.Parse(tbTemratureCorrection[i].Text.ToString()));
                }
                inputCommPortSingleton.GetInstance().writeMultiRegisters(modbusRegs);
                WriteDataOKInvoke umi = new WriteDataOKInvoke(WriteDataOK);
                if (this.IsHandleCreated)
                {
                    BeginInvoke(umi);
                }
            }
            catch (Exception ex)
            {
                LogClass.GetInstance().WriteExceptionLog(ex);
                //MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        private void buttonWTWrite_Click(object sender, EventArgs e)
        {
            buttonWTRead.Enabled = false;
            buttonWTWrite.Enabled = false;
            buttonMain.Enabled = false;
            writeDataThread = new Thread(new ThreadStart(WriteThread));
            writeDataThread.Start();
        }

        public void update()
        {
            inputCommPortSingleton.GetInstance().initComm();
            inputCommPortSingleton.GetInstance().openComm();
            buttonWTRead.Enabled = false;
            buttonWTWrite.Enabled = false;
            buttonMain.Enabled = false;
            updateDataThread = new Thread(new ThreadStart(DoUpdateRegs));
            updateDataThread.Start();
        }
    }
}
