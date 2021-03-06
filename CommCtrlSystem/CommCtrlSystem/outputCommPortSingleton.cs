﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Modbus.Device;
using Modbus.IO;
using Modbus.Utility;
using Modbus.Data;
using System.IO.Ports;
using System.IO;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace CommCtrlSystem
{
    public class outputCommPortSingleton
    {
        private static outputCommPortSingleton outputCommPort;
        private static readonly object locker = new object();
        SerialPort port;
        string OutputModbusType;
        IModbusSerialMaster master;

        private outputCommPortSingleton()
        {
        }

        public static outputCommPortSingleton GetInstance()
        {
            if (outputCommPort == null)
            {
                lock (locker)
                {
                    if (outputCommPort == null)
                    {
                        outputCommPort = new outputCommPortSingleton();
                    }
                }
            }
            return outputCommPort;
        }

        public static decimal GetNumber(string str)
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

        public void initComm()
        {
            lock (locker)
            {
                if (port == null)
                {
                    Configure cfg = null;
                    string cfgfile = System.IO.Path.Combine(Application.StartupPath, "cfg.json");
                    if (File.Exists(cfgfile))
                    {
                        cfg = JsonConvert.DeserializeObject<Configure>(File.ReadAllText(cfgfile));
                    }

                    port = new SerialPort(cfg.OutputSerialPortName);

                    port.BaudRate = (int)GetNumber(cfg.OutputSerialPortBaud);
                    port.DataBits = (int)GetNumber(cfg.OutputSerialPortDataBit);
                    if (cfg.OutputSerialPortParity == "None Parity")
                    {
                        port.Parity = Parity.None;
                    }
                    else if (cfg.OutputSerialPortParity == "Odd Parity")
                    {
                        port.Parity = Parity.Odd;
                    }
                    else
                    {
                        port.Parity = Parity.Even;
                    }

                    if (cfg.OutputSerialPortStopBit == "1 Stop Bit")
                    {
                        port.StopBits = StopBits.One;
                    }
                    else
                    {
                        port.StopBits = StopBits.Two;
                    }

                    OutputModbusType = cfg.OutputModbusType;
                }
            }
        }

        public void openComm()
        {
            lock (locker)
            {
                if (port != null && !port.IsOpen)
                {
                    port.Open();

                    // create modbus master
                    if (OutputModbusType == "RTU")
                    {
                        master = ModbusSerialMaster.CreateRtu(port);
                    }
                    else
                    {
                        master = ModbusSerialMaster.CreateAscii(port);
                    }
                }
            }
        }

        public void closeComm()
        {
            lock (locker)
            {
                if (port != null && port.IsOpen)
                {
                    port.Close();
                }
            }
        }

        public void readRegister(ref ModbusRegisters regs)
        {
            if (master == null)
            {
                return;
            }

            lock (locker)
            {
                regs.values = master.ReadHoldingRegisters(regs.slaveid, regs.startAddress, regs.numRegisters);
                for (int i = 0; i < regs.numRegisters; i++)
                {
                    regs.stReg[i].value = regs.values[i];
                }
            }
        }

        public void writeMultiRegisters(ModbusRegisters regs)
        {
            if (master == null)
            {
                return;
            }

            lock (locker)
            {
                for (int i = 0; i < regs.numRegisters; i++)
                {
                    regs.values[i] = regs.stReg[i].value;
                }

                master.WriteMultipleRegisters(regs.slaveid, regs.startAddress, regs.values);

            }
        }

        public void writeSingleRegister(ModbusRegisters regs, ushort value)
        {
            if (master == null)
            {
                return;
            }

            lock (locker)
            {
                master.WriteSingleRegister(regs.slaveid, regs.startAddress, value);
            }
        }
    }
}