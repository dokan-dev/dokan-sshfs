using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading;
using DokanNet;

namespace DokanSSHFS
{
    public partial class SettingForm : Form
    {
        private SSHFS sshfs;
        private DokanOptions opt;
        private string mountPoint;
        private int threadCount;
        private Settings settings = new Settings();
        private int selectedIndex = 0;
        private Thread dokan;
        private bool isUnmounted_ = false;

        public SettingForm()
        {
            InitializeComponent();
        }

        private void SettingForm_Load(object sender, EventArgs e)
        {
            FormBorderStyle = FormBorderStyle.FixedSingle;
            //notifyIcon1.Icon = SystemIcons.Application;
            notifyIcon1.Visible = true;
            SettingLoad();
        }

        private void open_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                privatekey.Text = openFileDialog1.FileName;
            }
            
        }

        private void usePassword_CheckedChanged(object sender, EventArgs e)
        {
            if (usePassword.Checked)
            {
                usePrivateKey.Checked = false;
                privatekey.Enabled = false;
                passphrase.Enabled = false;
                password.Enabled = true;
                open.Enabled = false;
            }
        }

        private void usePrivateKey_CheckedChanged(object sender, EventArgs e)
        {
            if (usePrivateKey.Checked)
            {
                usePassword.Checked = false;
                privatekey.Enabled = true;
                passphrase.Enabled = true;
                password.Enabled = false;
                open.Enabled = true;
            }
        }

        private void cancel_Click(object sender, EventArgs e)
        {
            notifyIcon1.Visible = false;
            Application.Exit();
        }

        private void connect_Click(object sender, EventArgs e)
        {
            this.Hide();

            int p = 22;
            
            sshfs = new SSHFS();
            opt = new DokanOptions();

            if (DokanSSHFS.DokanDebug)
                opt |= DokanOptions.DebugMode;
            opt |= DokanOptions.AltStream; // DokanOptions.KeepAlive always enabled.
            mountPoint = "n:\\";
            threadCount = 0;

            string message = "";

            if (host.Text == "")
                message += "Host name is empty\n";

            if (user.Text == "")
                message += "User name is empty\n";


            if (port.Text == "")
                message += "Port is empty\n";
            else
            {
                try
                {
                    p = Int32.Parse(port.Text);
                }
                catch(Exception)
                {
                    message += "Port format error\n";
                }
            }


            if (drive.Text.Length != 1)
            {
                message += "Drive letter is invalid\n";
            }
            else
            {
                char letter = drive.Text[0];
                letter = Char.ToLower(letter);
                if (!('e' <= letter && letter <= 'z'))
                    message += "Drive letter is invalid\n";

                mountPoint = string.Format("{0}:\\", letter);
                unmount.Text = "Unmount (" + mountPoint + ")";
            }

            threadCount = DokanSSHFS.DokanThread;

            if (message.Length != 0)
            {
                this.Show();
                MessageBox.Show(message, "Error");
                return;
            }

            DokanSSHFS.UseOffline = !withoutOfflineAttribute.Checked;

            sshfs.Initialize(
                user.Text,
                host.Text,
                p,
                usePrivateKey.Checked ? null : password.Text,
                usePrivateKey.Checked ? privatekey.Text : null,
                usePrivateKey.Checked ? passphrase.Text : null,
                root.Text,
                DokanSSHFS.SSHDebug);

            if (sshfs.SSHConnect())
            {
                unmount.Visible = true;
                mount.Visible = false;
                isUnmounted_ = false;

                MountWorker worker = null;
                if (disableCache.Checked)
                {
                    worker = new MountWorker(sshfs, opt, mountPoint, threadCount);
                }
                else
                {
                    worker = new MountWorker(new CacheOperations(sshfs), opt, mountPoint, threadCount);
                }

                dokan = new Thread(worker.Start);
                dokan.Start();
            }
            else
            {
                this.Show();
                MessageBox.Show("failed to connect", "Error");
                return;
            }

            MessageBox.Show("sshfs start", "info");
            
        }


        private void Unmount()
        {
            if (sshfs != null)
            {
                Debug.WriteLine(string.Format("SSHFS Trying unmount : {0}", mountPoint));

                try
                {
                    Dokan.RemoveMountPoint(mountPoint);
                    Debug.WriteLine("DokanReveMountPoint success\n");
                }
                catch (DokanException ex)
                {
                    Debug.WriteLine("DokanRemoveMountPoint failed: " + ex.Message + "\n");
                }
                // This should be called from Dokan, but not called.
                // Call here explicitly.
                sshfs.Unmounted(null);
            }
            unmount.Visible = false;
            mount.Visible = true;
        }


        class MountWorker
        {
            private IDokanOperations sshfs_;
            private DokanOptions opt_;
            private string mountPoint_;
            private int threadCount_;

            public MountWorker(IDokanOperations sshfs, DokanOptions opt, string mountPoint, int threadCount)
            {
                sshfs_ = sshfs;
                opt_ = opt;
                mountPoint_ = mountPoint;
                threadCount_ = threadCount;
            }

            public void Start()
            {
                System.IO.Directory.SetCurrentDirectory(Application.StartupPath);
                try
                {
                    sshfs_.Mount(mountPoint_, opt_, threadCount_);
                }
                catch (DokanException ex)
                {
                    MessageBox.Show(ex.Message, "Error");
                    Application.Exit();
                }
                Debug.WriteLine("DokanNet.Main end");
            }
        }

        
        private void exit_Click(object sender, EventArgs e)
        {
            notifyIcon1.Visible = false;

            if (!isUnmounted_)
            {
                Debug.WriteLine("unmount is visible");
                unmount.Visible = false;
                Unmount();
                isUnmounted_ = true;
            }

            Debug.WriteLine("SSHFS Thread Waitting");

            if (dokan != null && dokan.IsAlive)
            {
                Debug.WriteLine("doka.Join");
                dokan.Join();
            }
            
            Debug.WriteLine("SSHFS Thread End");

            Application.Exit();
        }

        
        private void unmount_Click(object sender, EventArgs e)
        {
            Debug.WriteLine("unmount_Click");          
            this.Unmount();
            isUnmounted_ = true;
        }

        private void save_Click(object sender, EventArgs e)
        {
            Setting s = settings[selectedIndex];

            s.Name = settingNames.Text;
            if (settingNames.Text == "New Setting")
                s.Name = settings.GetNewName();

            s.Host = host.Text;
            s.User = user.Text;
            try
            {
                s.Port = Int32.Parse(port.Text);
            }
            catch (Exception)
            {
                s.Port = 22;
            }

            s.PrivateKey = privatekey.Text;
            s.UsePassword = usePassword.Checked;
            s.Drive = drive.Text;
            s.ServerRoot = root.Text;
            s.DisableCache = disableCache.Checked;
            s.WithoutOfflineAttribute = withoutOfflineAttribute.Checked;

            settings.Save();

            SettingLoad();
            SettingLoad(selectedIndex);
        }



        private void settingNames_SelectedIndexChanged(object sender, EventArgs e)
        {
            selectedIndex = settingNames.SelectedIndex;
            SettingLoad(settingNames.SelectedIndex);
        }

        private void SettingLoad(int index)
        {
            Setting s = settings[index];

            host.Text = s.Host;
            user.Text = s.User;
            port.Text = s.Port.ToString();
            privatekey.Text = s.PrivateKey;
            password.Text = "";
            usePassword.Checked = s.UsePassword;
            usePrivateKey.Checked = !s.UsePassword;
            usePassword_CheckedChanged(null, null);
            usePrivateKey_CheckedChanged(null, null);

            disableCache.Checked = s.DisableCache;
            withoutOfflineAttribute.Checked = s.WithoutOfflineAttribute;

            drive.Text = s.Drive;
            root.Text = s.ServerRoot;         
        }

        private void SettingLoad()
        {
            settings.Load();
            settingNames.Items.Clear();
            int count = settings.Count;
            for (int i = 0; i < count; ++i)
            {
                settingNames.Items.Add(settings[i].Name);
            }
            settingNames.Items.Add("New Setting");
            settingNames.SelectedIndex = 0;
            SettingLoad(0);
        }

        private void delete_Click(object sender, EventArgs e)
        {
            settings.Delete(selectedIndex);
            settings.Save();
            SettingLoad();
        }

        private void mount_Click(object sender, EventArgs e)
        {
            unmount.Visible = false;
            this.Show();
        }
    }
}