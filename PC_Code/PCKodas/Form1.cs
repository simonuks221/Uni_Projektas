﻿using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.IO.Ports;
using System.IO;
using System.Threading;
using System.Reflection;
using System.Diagnostics;
using System.Linq;
using System.Diagnostics.Eventing.Reader;

namespace PCKodas
{
    
    public partial class Form1 : Form
    {
        private String[] ports;
        private SerialPort port;

        string connectedPort;

        public bool isConnected;

        int max_chart_elements = 100;
        int received_data_length = 0;

        const int received_data_max = 5000;
        int current_data_index = 0;
        int received_data_total = 0;
        int[] received_data = new int[received_data_max];

        SREvent<string> change_status_text_evt;
        SREvent<String[]> change_status_ports_evt;
        SREvent<string> update_total_amount_evt;


        private Thread reading_data_thread;
        private Thread writing_file_thread;

        //Chartai
        //https://learn.microsoft.com/en-us/previous-versions/dd456769(v=vs.140)?redirectedfrom=MSDN
        //https://learn.microsoft.com/en-us/previous-versions/dd489233(v=vs.140)?redirectedfrom=MSDN
        //https://learn.microsoft.com/de-de/dotnet/api/system.windows.forms.datavisualization.charting.chart?redirectedfrom=MSDN&view=netframework-4.8.1

        public Form1()
        {
            InitializeComponent();
            /* Setup shared events */
            change_status_text_evt = new SREvent<string>(ChangeDisplayedStatusText);
            change_status_ports_evt = new SREvent<String[]>(ChangeDisplayedPorts);
            update_total_amount_evt = new SREvent<string>(UpdateChartTotalAmount);
            /* Start application */
            InitUiLogic();
            InitDataReading();

            
        }

        void ChangeDisplayedStatusText(string connectionStatusTest)
        {
            this.Invoke(new MethodInvoker(delegate ()
            {
                ConnectionStatusLabel.Text = connectionStatusTest;
            }));
        }
        void ChangeDisplayedPorts(String[] ports)
        {
            this.Invoke(new MethodInvoker(delegate ()
            {
                comboBox1.Items.Clear();
                foreach (string port in ports)
                {
                    comboBox1.Items.Add(port);
                }

                if (ports.Length != 0)
                {
                    comboBox1.SelectedItem = ports[0];
                }
                else
                {
                    comboBox1.Items.Clear();
                }
            }));
        }

        void UpdateChartTotalAmount(string amount)
        {
            this.DataAmountLabel.Invoke(new MethodInvoker(delegate ()
            {
                DataAmountLabel.Text = amount;
            }));
        }
        private void InitUiLogic()
        {
            reading_data_thread = new Thread(new ThreadStart(UIManagerThread));
            reading_data_thread.IsBackground = true;
            reading_data_thread.Start();
            //UIManagerThread();
        }


        private void InitDataReading()
        {
            //TODO: might not need as serial callback is asynchronous already
            reading_data_thread = new Thread(new ThreadStart(ReadDataThread));
            reading_data_thread.IsBackground = true;
            reading_data_thread.Start();

        }

        private void UIManagerThread()
        {
            
            Object[] change_status_evts = { change_status_text_evt, change_status_ports_evt, update_total_amount_evt };

            WaitHandle[] change_status_events = new WaitHandle[change_status_evts.Length];
            for(int i = 0; i < change_status_evts.Length; i++)
            {
                PropertyInfo evt_property = change_status_evts[i].GetType().GetProperty("Event");
                if(evt_property != null)
                {
                    AutoResetEvent autoResetEvent = (AutoResetEvent)evt_property.GetValue(change_status_evts[i]);
                    change_status_events[i] = autoResetEvent;
                }
            }
            while (true)
            {
                int i = WaitHandle.WaitAny(change_status_events);
                MethodInfo callback_method = change_status_evts[i].GetType().GetMethod("Callback");
                if (callback_method != null) {
                    callback_method.Invoke(change_status_evts[i], null);
                }
                this.Invoke(new MethodInvoker(delegate ()
                {
                    this.Update();
                }));
            }
        }

        private void ReadDataThread()
        {
            GetAvailableComPorts();
        }

        void GetAvailableComPorts()
        {
            try
            {
                ports = SerialPort.GetPortNames();
                if (ports.Length > 0)
                {
                    change_status_ports_evt.Set(ports);
                    foreach (string portName in ports)
                    {
                        if (ConnectController(portName))
                        {
                            break;
                        }
                    }
                }
                else
                {
                    change_status_text_evt.Set("No COM ports available");
                    isConnected = false;
                }
            }
            catch
            {
                change_status_text_evt.Set("No COM ports available");
                isConnected = false;
            }
        }

        private bool ConnectController(string selectedPort)
        {
            if (!isConnected)
            {
                try
                {
                    port = new SerialPort(selectedPort, 921600, Parity.None, 8, StopBits.One);
                    port.Open();
                    port.DiscardInBuffer();
                    port.DataReceived += new SerialDataReceivedEventHandler(Port_DataReceived);
                    isConnected = true;
                    connectedPort = selectedPort;
                    change_status_text_evt.Set("Connected to port " + selectedPort);
                    return true;
                }
                catch
                {
                    isConnected = false;
                    change_status_text_evt.Set("Failed to connect");
                    return false;
                }
            }
            return false;
        }

        private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e) //Receive USB data
        {
            int count = port.BytesToRead;
            byte[] ByteArray = new byte[count];
            port.Read(ByteArray, 0, count);
            if (ByteArray.Length > 0) {
                Update_Chart(ByteArray);
            }
        }

        private void ConnectButton_Click(object sender, EventArgs e)
        {
            ConnectController(comboBox1.GetItemText(comboBox1.SelectedItem));
        }
        static int max_chart_val = -1000000;
        static int min_chart_val = 1000000;
        static bool got_lsb = false;
        static int full_value = 0;

        static byte lsb = 0;
        static byte msb = 0;
        void Update_Chart(byte[] new_values)
        {
            
            //Console.WriteLine(new_values.Count());

            for(int i = 0; i < new_values.Length; i++)
            {
                if(i < 2)
                {
                    continue;
                }
                //Console.WriteLine(new_values[i]);
                if ((new_values[i] & 0x80) == 0)
                {
                    /* Got LSB */
                    /* First get LSB then MSB */
                    lsb = new_values[i];
                }
                else
                {
                    /* Got MSB */
                    /* End conversion as got LSB and MSB */
                    msb = (byte)(new_values[i] & 0x7F);

                    full_value = (ushort)((msb << 7) | lsb);
                    /* If signed 10bit number received */
                    if(SignedNumberCheckBox.Checked)
                    {
                        /* Check if negative value */
                        if ((full_value & 0x2000) != 0)
                        //if ((full_value & 0x200) != 0)
                        {
                            // Convert to negative
                            //full_value -= 1024;
                            full_value -= 16384;
                        }
                    }
                    received_data_total++;
                    /* Check if needed save to file */
                    received_data[current_data_index] = full_value;
                    if (current_data_index >= received_data_max - 1)
                    {
                        SaveToFileData();
                        current_data_index = 0;
                    } else
                    {
                        current_data_index++;
                    }

                    

                    this.VoltageChart.Invoke(new MethodInvoker(delegate ()
                    {
                        if (!UpdateChartButton.Checked)
                        {
                            return;
                        }
                        /* Determine max/min chart Y values */
                        if (max_chart_val < full_value)
                        {
                            max_chart_val = full_value;
                        }

                        if (min_chart_val > full_value)
                        {
                            min_chart_val = full_value;
                        }
                        if (VoltageChart.Series[0].Points.Count == max_chart_elements)
                        {
                            VoltageChart.Series[0].Points.RemoveAt(0);
                        }
                        VoltageChart.Series[0].Points.AddY(full_value);
                        /* Update chart Y range every 10 values */
                        if(received_data_total % 10 == 0)
                        {
                            /* Check if min/max point values are valid */
                            if(min_chart_val == max_chart_val)
                            {
                                min_chart_val = min_chart_val - 1;
                                max_chart_val = max_chart_val + 1;
                            }
                            VoltageChart.ChartAreas[0].AxisY.Minimum = min_chart_val;
                            VoltageChart.ChartAreas[0].AxisY.Maximum = max_chart_val;
                        }
                    }));
                }

            }
            this.VoltageChart.Invoke(new MethodInvoker(delegate ()
            {
                if (!UpdateChartButton.Checked)
                {
                    return;
                }
               
            }));
            update_total_amount_evt.Set("Total: " + received_data_total.ToString());
        }

        private void SaveToFileData()
        {
            writing_file_thread = new Thread(new ThreadStart(WritingFileThread));
            writing_file_thread.IsBackground = true;
            writing_file_thread.Start();
        }

        private void WritingFileThread()
        {
            string currentDate = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");
            string fileName = $"Received_data_{0}_{currentDate}.txt";

            TextWriter txt = new StreamWriter(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName));
            foreach (int d in received_data)
            {
                txt.Write(d.ToString() + "\n");
            }
            txt.Close();
        }
        private void SaveToFileButton_Click(object sender, EventArgs e)
        {
            SaveToFileData();
        }
    }
}
