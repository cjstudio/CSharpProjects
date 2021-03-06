﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace CHCJ
{
    public class CJConnect
    {
        private const string confPath = "../../config/connect_conf.xml";
        public const string XMLHeader = "<?xml version=\"1.0\" encoding=\"utf-8\" ?>";
        public const int max_context_bytes = 2048;
        private int TIMEOUT = 5000;   // 重传间隔时间
        private int MAX_REPEAT_TIMES = 3;   // 最大重传次数
        public int times_repeat = 0;    // 已发送的次数
        public IPEndPoint end_point_server;     // 服务器端
        public IPAddress ip_server;
        private UdpClient udp_server;    // 服务器端套接字
        private UdpClient udp_local;     // 本地套接字
        private TcpClient tcp_client;
        private NetworkStream tcp_netstream;
        private int port_udp_local = 2232;      // 本地端口
        private int port_tcp_local = 2233;      // 本地端口
        public int port_udp_server = 3333;
        public int port_tcp_server = 2222;
        private SendState send_state=SendState.NONE; // 发送的状态
        public CJMsg msg_send, msg_receive;       // 要发送和已接收的消息

        private Thread t_moniter, t_sender;     // 发送及监听的线程
        delegate void CB_V_V();
        delegate void CB_V_I(int num);
        CB_V_V cb_moniter;
        CB_V_V cb_sender;

        public DelegateConnectHandle ReceiveOver;
//         public  enum SendState
//         {
//             OK = 0,     // 发送成功，并收到回复
//             OUTTIME,    // 发送成功，但等待回复超时
//             LOCAL_ERROR,    // 本地错误
//             SERVER_ERROR,   // 服务端接收数据错误
//             CONTENT_TOO_LONG,   // 要发送的数据过长
//             CONTENT_ERROR,   // 连接异常
//             SENDING,        // 发送中
//             WAITING_REPLY,  // 发送成功，等待回复
//             NONE
//         }
        public CJConnect()
        {
            getConf();
            initSocket();
        }
        public CJConnect(string type)
        {
            //getConf();
            if (type == "tcp")
            {
                while (true)    // 防止端口被占用
                {
                    try
                    {
                        tcp_client = new TcpClient(
                            new IPEndPoint(IPAddress.Parse("127.0.0.1"), port_tcp_local));
                        break;
                    }
                    catch (Exception)
                    {
                        port_tcp_local++;
                    }
                }
            }
            else
            {
                while (true)    // 防止端口被占用
                {
                    try
                    {
                        udp_local = new UdpClient(
                            new IPEndPoint(IPAddress.Parse("127.0.0.1"), port_udp_local));
                        break;
                    }
                    catch (Exception)
                    {
                        port_udp_local++;
                    }
                }
            }
        }

        private void getConf() // 从文件获取基本配置 
        {
            ip_server = CJConfig.ServerIp;
            port_udp_server = CJConfig.ServerUdpPort;
            port_tcp_server = CJConfig.ServerTcpPort;
            end_point_server = CJConfig.ServerUdpEndPoint;
            TIMEOUT = CJConfig.TimeOut;
            MAX_REPEAT_TIMES = CJConfig.MaxRepeatTimes;
        }

        private void initSocket() // 初始化本地和服务端套接字
        {
            while (true)    // 防止端口被占用
            {
                try
                {
                    udp_local = new UdpClient(
                        new IPEndPoint(IPAddress.Parse("127.0.0.1"),port_udp_local));
                    break;
                }
                catch (Exception)
                {
                    port_udp_local++;
                }
            }
            while (true)    // 防止端口被占用
            {
                try
                {
                    tcp_client = new TcpClient(
                        new IPEndPoint(IPAddress.Parse("127.0.0.1"), port_tcp_local));
                    break;
                }
                catch (Exception)
                {
                    port_tcp_local++;
                }
            }
            //udp_server = new UdpClient(end_point_server);
        }
        private void initThread() // 线程，委托等的初始化 
        {
            cb_moniter = new CB_V_V(do_udp_monitor);
            t_moniter = new Thread(new ThreadStart(cb_moniter));
            t_moniter.Name = "ThreadMoniter";
            cb_sender = new CB_V_V(do_udp_sendMsg);
            t_sender = new Thread(new ThreadStart(cb_sender));
            t_sender.Name = "ThreadSender";
        }
        private int setServerEndPoint(string ip, int port) 
        {
            try
            {
                end_point_server = new IPEndPoint(IPAddress.Parse(ip), port);
                return 0;
            }
            catch (Exception)
            {
                return -1;
            };
        }
        
        public void udp_sendMsg()
        {
            //t_sender.Start(times_repeat++);
            fun_do_udp_sendMsg(times_repeat);
            times_repeat++;
            if (send_state == SendState.WAITING_REPLY)
            {
                initThread();
                t_moniter.Start();
                t_sender.Start();
            }
        }
        private void do_udp_sendMsg()
        {
            //while (times_repeat <= MAX_REPEAT_TIMES)
            object o = new object();
            while (send_state == SendState.WAITING_REPLY && 
                times_repeat <= MAX_REPEAT_TIMES)
            {
                if (t_moniter.Join(TIMEOUT))
                {
                    return;
                }
                else
                {
                    t_moniter.Suspend();
                    t_moniter = new Thread(new ThreadStart(cb_moniter));
                    lock (o)
                    {
                        fun_do_udp_sendMsg(times_repeat++);
                    }
                    t_moniter.Start();
                }
            }
            if (times_repeat >= MAX_REPEAT_TIMES)
            {
                Thread.Sleep(TIMEOUT);
                send_state = SendState.OUTTIME;
                t_moniter.Abort();
                lock (o)
                {
                    times_repeat++;
                }
                receiveOver();
            }
        }
        private void do_udp_monitor()
        {
            object o = new object();
            lock (o)
            {
                while (times_repeat <= MAX_REPEAT_TIMES)
                {
                    try
                    {
                        byte[] b = udp_local.Receive(ref end_point_server);
                        string s = Encoding.UTF8.GetString(b);
                        msg_receive = new CJMsg(s);
                        if (CHCJ.MsgType.CS_REPLY == msg_receive.getMsgType())
                        {
                            send_state = SendState.OK;
                            receiveOver();
                            break;
                        }
                        else
                        {
                            send_state = SendState.SERVER_ERROR;
                            receiveOver();
                            break;
                        }
                    }
                    catch (Exception )
                    {
                        ;
                    }
                }
            }
        }
        private void fun_do_udp_sendMsg(int num)
        {
            object o = new object();
            lock (o)
            {
                udp_local = new UdpClient();
                try
                {
                    byte[] b = Encoding.UTF8.GetBytes(msg_send.ToString());
                    if (b.Length > max_context_bytes)
                    {
                        send_state = SendState.CONTENT_TOO_LONG;
                        return;
                    }
                    udp_local.Connect(end_point_server);
                    udp_local.Send(b, b.Length);
                    send_state = SendState.WAITING_REPLY;
                }
                catch (Exception)
                {
                    send_state = SendState.LOCAL_ERROR;
                    receiveOver();
                }
            }
        }

        public void tcp_sendMsg()
        {
            try
            {
                tcp_netstream = tcp_client.GetStream();  //获取绑定的网络数据流
                try
                {
                    byte[] sendData = new byte[2048];
                    sendData = Encoding.Default.GetBytes(msg_send.ToString());
                    tcp_netstream.Write(sendData, 0, sendData.Length);
                }
                catch (Exception )
                {
                    send_state = SendState.SERVER_ERROR;
                    receiveOver();
                }
                Thread revThread = new Thread(tcp_moniter);
                revThread.Start();
            }
            catch (Exception )
            {
                send_state = SendState.CONTENT_ERROR;
                receiveOver();
            }
        }
        private void tcp_moniter()
        {
             while (true)
            {
                int readLen = tcp_client.Available;
                if (readLen > 0)
                {
                    byte[] getData = new byte[2048];
                    tcp_netstream.Read(getData, 0, getData.Length);
                    string getMsg = Encoding.UTF8.GetString(getData);
                    msg_receive = new CJMsg(getMsg);
                    send_state = SendState.OK;
                    receiveOver();
                }
            }
        }

        public void receiveOver() // 接收到数据后，触发事件 
        {
            if (ReceiveOver != null)
            {
                CJReceiveOverEvent e = new CJReceiveOverEvent();
                e.sendState = this.send_state;
                e.Msg = this.msg_receive;
                ReceiveOver(this, e);
            }
        }
        public void receiveOver(string remark) // 接收到数据后，触发事件 
        {
            if (ReceiveOver != null)
            {
                CJReceiveOverEvent e = new CJReceiveOverEvent();
                e.sendState = this.send_state;
                e.Msg = this.msg_receive;
                e.Remark = remark;
                ReceiveOver(this, e);
            }
        }
        private void test()
        {
            ;
        }
    }
}
