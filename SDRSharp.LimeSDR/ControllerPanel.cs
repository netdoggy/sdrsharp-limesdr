/*
 * Based on project from https://github.com/jocover/sdrsharp-limesdr
 * 
 * modifications by YT7PWR 2018 https://github.com/GoranRadivojevic/sdrsharp-limesdr
 * modifications by netdog 2019 https://github.com/netdoggy/sdrsharp-limesdr
 */

using System;
using System.Windows.Forms;

namespace SDRSharp.LimeSDR
{
    public class ControllerPanel : UserControl
    {
        private Label label1;
        private Label lblTempValue;
        private LimeSDRIO limeSDRIO;

        public ControllerPanel(LimeSDRIO limeSDRIO)
        {
            this.limeSDRIO = limeSDRIO;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.label1 = new System.Windows.Forms.Label();
            this.lblTempValue = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(-3, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(70, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Temperature:";
            // 
            // lblTempValue
            // 
            this.lblTempValue.AutoSize = true;
            this.lblTempValue.Location = new System.Drawing.Point(77, 0);
            this.lblTempValue.Name = "lblTempValue";
            this.lblTempValue.Size = new System.Drawing.Size(14, 13);
            this.lblTempValue.TabIndex = 1;
            this.lblTempValue.Text = "-°";
            // 
            // ControllerPanel
            // 
            this.Controls.Add(this.lblTempValue);
            this.Controls.Add(this.label1);
            this.Name = "ControllerPanel";
            this.Size = new System.Drawing.Size(150, 20);
            this.Load += new System.EventHandler(this.ControllerPanel_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private void ControllerPanel_Load(object sender, System.EventArgs e)
        {

        }

        internal void setTemp(string text)
        {
            lblTempValue.Text = text;
        }
    }
}