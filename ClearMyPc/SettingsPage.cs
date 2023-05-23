using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ClearMyPc
{
    public partial class SettingsPage : Form
    { 
        public SettingsPage()
        {
            InitializeComponent();
        }
        bool allChecked = false;
        bool clear = false;
        private void selectAll_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox15.Checked)
            {
                allChecked = true;
                checkBox1.Checked = true;
                checkBox2.Checked = true;
                checkBox3.Checked = true;
                checkBox4.Checked = true;
                checkBox5.Checked = true;
                checkBox6.Checked = true;
                checkBox7.Checked = true;
                checkBox8.Checked = true;
                checkBox9.Checked = true;
                checkBox10.Checked = true;
                checkBox11.Checked = true;
                checkBox12.Checked = true;
                checkBox13.Checked = true;
                checkBox14.Checked = true;
                checkBox15.Checked = true;
            }
            else if (!checkBox15.Checked && allChecked)
            {
                clear = true;
                foreach (var checkBox in Controls.OfType<CheckBox>()) 
                    checkBox.Checked = false; 
            }
        }

        private void checkBoxes_CheckedChanged(object sender, EventArgs e)
        {
            if (!clear) checkBox15.Checked = false;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            string extensions = "";
            foreach (var checkBox in Controls.OfType<CheckBox>())
            {
                if (checkBox.Checked)
                    extensions += checkBox.Text + ",";
            }
            if (string.IsNullOrEmpty(textBox1.Text))
            {
                MessageBox.Show("Select a folder path to scan!");
            }
            else if(string.IsNullOrEmpty(extensions))
            { 
                MessageBox.Show("Select at least one extension to scan!");
            }
            else
            {
                extensions = extensions.Remove(extensions.Length - 1);
                extensions += "*" + textBox1.Text;
                extensions = extensions.Replace("All,", "");
                ScanPage.Singleton.extensions = extensions;
                ScanPage.Singleton.path= textBox1.Text;
                MessageBox.Show("Settings Saved!");
            } 
        }

        private void pathBrowser_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                DialogResult result = fbd.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    textBox1.Text = fbd.SelectedPath;
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        { 
        }
    }
}
