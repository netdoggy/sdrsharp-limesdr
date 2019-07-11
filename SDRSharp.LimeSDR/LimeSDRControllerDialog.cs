/*
 * Based on project from https://github.com/jocover/sdrsharp-limesdr
 * 
 * modifications by YT7PWR 2018 https://github.com/GoranRadivojevic/sdrsharp-limesdr
 * modifications by netdog 2019 https://github.com/netdoggy/sdrsharp-limesdr
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using SDRSharp.Radio;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SDRSharp.LimeSDR
{
    public partial class LimeSDRControllerDialog : Form
    {
        private readonly LimeSDRIO _owner;
        private bool _initialized;
        public double _sampleRate = 1.5 * 1e6;
        public double _freqDiff = 0.0;
        private Dictionary<int, string> _sampleRates = new Dictionary<int, string>();
       
        public LimeSDRControllerDialog(LimeSDRIO owner)
        {
            try
            {
                InitializeComponent();
                float dpi = this.CreateGraphics().DpiX;
                float ratio = dpi / 96.0f;
                string font_name = this.Font.Name;
                float size = (float)(8.25 / ratio);
                System.Drawing.Font new_font = new System.Drawing.Font(font_name, size);
                this.Font = new_font;
                this.PerformAutoScale();
                this.PerformLayout();
                _owner = owner;
                InitSampleRates();
                GetDeviceList();

                _initialized = true;

                comboRadioModel.Text = Utils.GetStringSetting("LimeSDR model", "");
                tbLimeSDR_Gain.Value = Utils.GetIntSetting("LimeSDR Gain", 40);
                gainBar_Scroll(this, EventArgs.Empty); // force resave param to lime device?                 
                lblLimeSDR_GainDB.Text = tbLimeSDR_Gain.Value.ToString()+"dB";
                samplerateComboBox.SelectedValue = Int32.Parse(Utils.GetStringSetting("LimeSDR SampleRate", "768000"));
                LPBWcomboBox.Text = Utils.GetStringSetting("LimeSDR LPBW", "1.5MHz");
                rx0.Checked = Utils.GetBooleanSetting("LimeSDR RX0");
                rx1.Checked = Utils.GetBooleanSetting("LimeSDR RX1");
                ant_l.Checked = Utils.GetBooleanSetting("LimeSDR ANT_L");
                ant_h.Checked = Utils.GetBooleanSetting("LimeSDR ANT_H");
                ant_w.Checked = Utils.GetBooleanSetting("LimeSDR ANT_W");
                udSpecOffset.Value = (decimal)Utils.GetDoubleSetting("LimeSDR SpecOffset", 1);
                udSpecOffset_ValueChanged(this, EventArgs.Empty); // force resave param  SpecOffset to Onwer (device) obj
                udFrequencyDiff.Value = (decimal)Utils.GetDoubleSetting("LimeSDR Frequency diff.", 0.0);
                RefreshLimeSdrTemp();
                toolTip_Gain.SetToolTip(lblLimeSDR_GainDB, "");
                toolTip_Gain.SetToolTip(tbLimeSDR_Gain, "");
                toolTip_Gain.SetToolTip(tbLimeSDR_LNAGain, "");
                toolTip_Gain.SetToolTip(tbLimeSDR_PGAGain, "");
                toolTip_Gain.SetToolTip(tbLimeSDR_TIAGain, "");

            }
            catch (Exception ex)
            {
                Debug.Write(ex.ToString());
            }
        }

        private bool Initialized
        {
            get
            {
                return _initialized && _owner != null;
            }
        }

        private void InitSampleRates()
        {
            int[] sampleRates =
            {
                192000,
                384000,
                768000,
                1536000,
                2304000,
                3072000,
                6144000,
                8192000,
                12288000,
                19200000,
                24576000,
                30000000,
                35000000,
                40000000,
                49152000,
                55296000
            };

            foreach (var sr in sampleRates) {
                var displayValue = Math.Round(sr / 1e6f, 4);
                _sampleRates.Add(sr, displayValue.ToString() + "M");
            }

            
            samplerateComboBox.DataSource = new BindingSource(_sampleRates, null);
            samplerateComboBox.DisplayMember = "Value";
            samplerateComboBox.ValueMember = "Key";
            samplerateComboBox.SelectedValue = 2304000;
            // string key = ((KeyValuePair<string, string>)samplerateComboBox.SelectedItem).Key;
            // string value = ((KeyValuePair<string, string>)samplerateComboBox.SelectedItem).Value;
        }

        public double LPBW
        {
            get
            {
                var commaCulture = new System.Globalization.CultureInfo("en")
                {
                    NumberFormat = { NumberDecimalSeparator = "." }
                };
                return (double)(UInt32)(double.Parse(LPBWcomboBox.Text.Replace("MHz", ""), commaCulture) * 1e6);
            }
        }

        private void samplerateComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            int value = ((KeyValuePair<int, string>)samplerateComboBox.SelectedItem).Key;

            if (!Initialized)
            {
                _sampleRate = value; // double.Parse((samplerateComboBox.Text));
                return;
            }

            try
            {
                _sampleRate = value; //  double.Parse((samplerateComboBox.Text));
                Utils.SaveSetting("LimeSDR SampleRate", value);
                _owner._sampleRate = _sampleRate;

                if (_owner.Device != null)
                    _owner.Device.SampleRate = _sampleRate;
            }
            catch
            {
                _sampleRate = 1.5 * 1e6;
            }
        }

        private void close_Click(object sender, EventArgs e)
        {
            Hide();
        }

        private void LimeSDRControllerDialog_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }


        private void gainBar_Scroll(object sender, EventArgs e)
        {
            if (!Initialized)
            {
                return;
            }

           
            _owner.Gain = tbLimeSDR_Gain.Value; // write to device
            lblLimeSDR_GainDB.Text = tbLimeSDR_Gain.Value + "dB";
            Utils.SaveSetting("LimeSDR Gain", (int)tbLimeSDR_Gain.Value);

            // read from device if open
            tbLimeSDR_LNAGain.Value = _owner.LNAgain;
            RefreshLabelLnaGain();

            tbLimeSDR_TIAGain.Value = _owner.TIAgain;
            RefreshLabelTiaGain();

            tbLimeSDR_PGAGain.Value = _owner.PGAgain;
            RefreshLabelPgaGain();
        }


        public void RefreshFormAllGains()
        {
            if (_owner == null)
                return;
            // read data from device 
            tbLimeSDR_LNAGain.Value = _owner.LNAgain;
            RefreshLabelLnaGain();

            tbLimeSDR_TIAGain.Value = _owner.TIAgain;
            RefreshLabelTiaGain();

            tbLimeSDR_PGAGain.Value = _owner.PGAgain;
            RefreshLabelPgaGain();
        }

        private void rx0_CheckedChanged(object sender, EventArgs e)
        {
            if (!Initialized)
            {
                return;
            }

            if (rx0.Checked)
                _owner.Channel = 0;

            Utils.SaveSetting("LimeSDR RX0", rx0.Checked);
        }

        private void rx1_CheckedChanged(object sender, EventArgs e)
        {
            if (!Initialized)
            {
                return;
            }

            if (rx1.Checked)
                _owner.Channel = 1;

            Utils.SaveSetting("LimeSDR RX1", rx1.Checked);
        }

        private void ant_h_CheckedChanged(object sender, EventArgs e)
        {
            if (!Initialized)
            {
                return;
            }

            if (ant_h.Checked)
                _owner.Antenna = 1;

            Utils.SaveSetting("LimeSDR ANT_H", ant_h.Checked);
        }

        private void ant_l_CheckedChanged(object sender, EventArgs e)
        {
            if (!Initialized)
            {
                return;
            }

            if (ant_l.Checked)
                _owner.Antenna = 2;

            Utils.SaveSetting("LimeSDR ANT_L", ant_l.Checked);
        }

        private void ant_w_CheckedChanged(object sender, EventArgs e)
        {
            if (!Initialized)
            {
                return;
            }

            if (ant_w.Checked)
                _owner.Antenna = 3;

            Utils.SaveSetting("LimeSDR ANT_W", ant_w.Checked);
        }

        private void LPBWcomboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!Initialized)
            {
                return;
            }

            try
            {
                _owner.LPBW = (double)(UInt32)(double.Parse(LPBWcomboBox.Text.Replace("MHz", "")) * 1e6);
                Utils.SaveSetting("LimeSDR LPBW", LPBWcomboBox.Text);
            }
            catch
            {
                _owner.LPBW = 1.5 * 1e6;
            }
        }

        private void udSpecOffset_ValueChanged(object sender, EventArgs e)
        {
            if (!Initialized)
            {
                return;
            }

            try
            {
                if (_owner != null && _owner.Device != null)
                    _owner.Device.SpectrumOffset = (float)udSpecOffset.Value;

                if (_owner != null)
                    _owner.SpecOffset = (float)udSpecOffset.Value;

                Utils.SaveSetting("LimeSDR SpecOffset", udSpecOffset.Value.ToString());
            }
            catch
            {

            }
        }

        private void btnRadioRefresh_Click(object sender, EventArgs e)
        {
            try
            {
                if (_owner != null && !_owner._isStreaming)
                {
                    comboRadioModel.Items.Clear();
                    GetDeviceList();
                    comboRadioModel.Text = Utils.GetStringSetting("LimeSDR model", "");
                }

                GetLimeSDRDeviceInfo();

            }
            catch (Exception ex)
            {
                Debug.Write(ex.ToString());
            }
        }

        unsafe public void GetDeviceList()
        {
            try
            {
                ASCIIEncoding ascii = new ASCIIEncoding();
                byte[] info = new byte[2048];
                int count = 0;

                fixed (byte* deviceList = &info[0])
                {
                    count = NativeMethods.LMS_GetDeviceList(deviceList);
                }

                for (int i = 0; i < count; i++)
                    comboRadioModel.Items.Add(ascii.GetString(info, i * 256, 256).Trim('\0'));
            }
            catch (Exception ex)
            {
                Debug.Write(ex.ToString());
            }
        }

        private void comboRadioModel_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                string[] data = new string[5];
                data = comboRadioModel.SelectedItem.ToString().Split(',');
                txtRadioName.Text = data[0];
                txtRadioSerialNo.Text = data[3].Replace("serial=", "");
                txtModule.Text = data[2].Replace("module=", "");
                _owner.RadioName = comboRadioModel.SelectedItem.ToString();
                Utils.SaveSetting("LimeSDR model", comboRadioModel.Text);

                txtRadioModel.Text = "";
                txtFirm_version.Text = "";
                txtSerialNumber.Text = "";
                txtGatewareVersion.Text = "";
                txtLimeSuiteVersion.Text = "";
            }
            catch (Exception ex)
            {
                Debug.Write(ex.ToString());
            }
        }

        private void udFrequencyDiff_ValueChanged(object sender, EventArgs e)
        {
            try
            {
                _freqDiff = (double)udFrequencyDiff.Value * 1e3;

                if (_owner != null)
                    _owner.FreqDiff = (double)udFrequencyDiff.Value * 1e3;

                Utils.SaveSetting("LimeSDR Frequency diff.", udFrequencyDiff.Value.ToString());
            }
            catch (Exception ex)
            {
                Debug.Write(ex.ToString());
            }
        }

        private void tbLimeSDR_LNAGain_Scroll(object sender, EventArgs e)
        {


            try
            {                
                RefreshLabelLnaGain();
                if (_owner != null)
                {
                    _owner.LNAgain = (ushort)tbLimeSDR_LNAGain.Value;
                    RefreshFormGain();
                }

            }
            catch (Exception ex)
            {
                Debug.Write(ex.ToString());
            }
        }

        private void tbLimeSDR_TIAGain_Scroll(object sender, EventArgs e)
        {
            try
            {
                RefreshLabelTiaGain();

                if (_owner != null)
                {
                    _owner.TIAgain = (ushort)tbLimeSDR_TIAGain.Value;
                    RefreshFormGain();
                }

            }
            catch (Exception ex)
            {
                Debug.Write(ex.ToString());
            }
        }

        private void RefreshFormGain()
        {
            tbLimeSDR_Gain.Value = (int)_owner.Gain;
            lblLimeSDR_GainDB.Text = tbLimeSDR_Gain.Value.ToString() + "dB";
        }

        private void RefreshLabelLnaGain()
        {
            string[] LnaValues = { "N/A", "-30 (0dB)", "-27 (3dB)", "-24 (6dB)", "-21 (9dB)", "-18 (12dB)", "-15 (15dB)", "-12 (18dB)", "-9 (21dB)", "-6 (24dB)", "-5 (25dB)", "-4 (26dB)", "-3 (27dB)", "-2 (28)", "-1 (29dB)", "Max (30dB)" };
            lblLimeSDR_LNAGain.Text = LnaValues[tbLimeSDR_LNAGain.Value];
        }

        private void RefreshLabelTiaGain()
        {
            string[] TiaValues = { "N/A", "-12 (0dB)", "-3 (9dB)", "Max (12dB)" };
            lblLimeSDR_TIAGain.Text = TiaValues[tbLimeSDR_TIAGain.Value];
        }

        private void RefreshLabelPgaGain()
        {
            lblLimeSDR_PGAGain.Text = (-12 + tbLimeSDR_PGAGain.Value).ToString() + "dB";
        }


        private void tbLimeSDR_PGAGain_Scroll(object sender, EventArgs e)
        {
            try
            {
                RefreshLabelPgaGain();

                if (_owner != null)
                {
                    _owner.PGAgain = (ushort)tbLimeSDR_PGAGain.Value;
                    RefreshFormGain();
                }

            }
            catch (Exception ex)
            {
                Debug.Write(ex.ToString());
            }
        }

        public void RefreshLimeSdrTemp()
        {
            var temp = _owner.Device.Temp();
            //label_Temp.Text = 
            tb_Temperature.Text = temp == 0 ? "N/A" : Math.Round(temp, 2).ToString() + " C";
        }
        public unsafe void GetLimeSDRDeviceInfo()
        {
            try
            {
                if (_owner.LimeSDR_Device != IntPtr.Zero)
                {
                    lms_dev_info_t info = new lms_dev_info_t();

                    info.deviceName = new char[32];
                    info.expansionName = new char[32];
                    info.firmwareVersion = new char[16];
                    info.hardwareVersion = new char[16];
                    info.protocolVersion = new char[16];
                    info.boardSerialNumber = 0;
                    info.gatewareVersion = new char[16];
                    info.gatewareTargetBoard = new char[32];
                    IntPtr deviceInfo;

                    deviceInfo = (IntPtr)NativeMethods.LMS_GetDeviceInfo(_owner.LimeSDR_Device);
                    byte[] buff = new byte[168];
                    Marshal.Copy(deviceInfo, buff, 0, 168);
                    ASCIIEncoding ascii = new ASCIIEncoding();
                    string s = ascii.GetString(buff);
                    string deviceName = ascii.GetString(buff, 0, 32).Trim('\0');
                    string expansionName = ascii.GetString(buff, 32, 32).Trim('\0');
                    string firmwareVersion = ascii.GetString(buff, 64, 16).Trim('\0');
                    string hardwareVersion = ascii.GetString(buff, 80, 16).Trim('\0');
                    string protocolVersion = ascii.GetString(buff, 96, 16).Trim('\0');
                    UInt64 serial = 0;

                    for (int i = 8; i > 0; i--)
                    {
                        serial += buff[111 + i];

                        if (i > 1)
                            serial = serial << 8;
                    }

                    string boardSerialNumber = serial.ToString("X");
                    string gatewareVersion = ascii.GetString(buff, 120, 16).Trim('\0');
                    string gatewareTargetBoard = ascii.GetString(buff, 136, 32).Trim('\0');

                    IntPtr libVersion;
                    libVersion = (IntPtr)NativeMethods.LMS_GetLibraryVersion();
                    string limeSuiteVersion = Marshal.PtrToStringAnsi(libVersion);

                    txtRadioModel.Text = deviceName;
                    txtFirm_version.Text = firmwareVersion;
                    txtSerialNumber.Text = boardSerialNumber;
                    txtGatewareVersion.Text = gatewareVersion;
                    txtLimeSuiteVersion.Text = limeSuiteVersion;
                    RefreshLimeSdrTemp();

                }
            }
            catch (Exception ex)
            {
                Debug.Write(ex.ToString());
                txtRadioModel.Text = "";
                txtFirm_version.Text = "";
                txtSerialNumber.Text = "";
                txtGatewareVersion.Text = "";
                txtLimeSuiteVersion.Text = "";
            }

        }

        private void groupBox3_Enter(object sender, EventArgs e)
        {

        }

        private void LimeSDRControllerDialog_Load(object sender, EventArgs e)
        {
            RefreshLimeSdrTemp();
            //if (_owner.Device != null)
            // label_Temp.Text = _owner.Device.Temp().ToString();
        }

        private void toolTip_Gain_Popup(object sender, PopupEventArgs e)
        {

        }

        private void label_Temp_Click(object sender, EventArgs e)
        {

        }

        private void timerTemp_Tick(object sender, EventArgs e)
        {
            RefreshLimeSdrTemp();
        }

        private void label6_Click(object sender, EventArgs e)
        {

        }

        private void lblLimeSDR_PGAGain_Click(object sender, EventArgs e)
        {

        }
    }
}
