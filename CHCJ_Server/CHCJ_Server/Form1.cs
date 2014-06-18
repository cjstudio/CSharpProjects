using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using ServiceStack.Redis;

namespace CHCJ_Server
{
    public partial class Form1 : Form
    {
        private int port_udp_moniter = 2222;
        private int port_tcp_moniter = 2223;
        private Thread t_udp_moniter;
        private Thread t_tcp_moniter;
        public CJLog log;
        public RedisClient redis;
        public CJServer server;
        public Form1()
        {
            InitializeComponent();
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            server = new CJServer();
            server.ReceiveOver += new DelegateConnectHandle(solveReceive);
            log = new CJLog(CJLog.LogLevel.ALL,CJLog.LogModel.TEXT_BOX_SHOW);
            log.setControl(textBox1);
            try
            {
                redis = new RedisClient("127.0.0.1", 6379);
            }
            catch (Exception)
            {
                ;
            }
        }
        public void solveReceive(object sender, CJReceiveOverEvent e)
        {
            if (e.Level!=null)
            {
                log.loging(e.Str,e.Level);
            }
            else if (e.Msg != null)
            {
                log.loging(e.Msg.ToString());
            }
        }
        private void startServer()
        {
            t_udp_moniter = new Thread(new ThreadStart(()=>
                {
                    do_moniter();
                }
                ));
            t_udp_moniter.Start();
        }
        private void stopServer()
        {
            t_udp_moniter.Abort();
        }
        private void do_moniter()
        {
            ;
        }

        private void button1_Click(object sender, EventArgs e)
        {string XMLHeader = "<?xml version=\"1.0\" encoding=\"utf-8\" ?>";
            string MsgFrame =
                @"<cjstudio>
  <From>
    <IPAddress></IPAddress>
    <Port>3</Port>
    <UserID>10003</UserID>
    <Name></Name>
  </From>
  <To>
    <IPAddress></IPAddress>
    <Port>3</Port>
    <UserID>10001</UserID>
    <Name></Name>
  </To>
  <Data>
    <Text>
hello test;
    </Text>
  </Data>
  <Hash>
  </Hash>
</cjstudio>";
            CJMsg msg = new CJMsg(XMLHeader+MsgFrame);
            server.db_addMsgNoReceive(msg.getMsgPeerTo().id, msg.ToString());
        }

        private void button2_Click(object sender, EventArgs e)
        {
            //textBox3.Text = Encoding.UTF8.GetString( server.redis.Get("tmp"));
        }

        private void 启动SToolStripMenuItem_Click(object sender, EventArgs e)
        {
            server.startServer();
        }
        private void 停止EToolStripMenuItem_Click(object sender, EventArgs e)
        {
            server.stopServer();
        }
        private void 重启RToolStripMenuItem_Click(object sender, EventArgs e)
        {
            server.stopServer();
            server.startServer();
        }
        private void 退出QToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Environment.Exit(0);
        }
        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            System.Environment.Exit(0);
        }
    }
}
