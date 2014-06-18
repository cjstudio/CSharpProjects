using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using System.Net.Sockets;
using System.Net;
using ServiceStack;
using ServiceStack.Redis;

namespace CHCJ_Server
{
    public class CJServer
    {
        public bool server_state = false;
        private int port_udp_monitor = 3333;
        private int port_tcp_monitor = 2222;
        private Thread t_udp_monitor;
        private Thread t_tcp_monitor;
        private TcpListener tcp_listener;
        private UdpClient udp_listener;
        private IPEndPoint udp_endpoint;
        IPEndPoint udp_client_IPEnd;
        public RedisClient redis; 
        public DelegateConnectHandle ReceiveOver;
        public int online_timeout=300;

        public CJServer()
        {
            init();
        }
        private void init()
        {
            udp_endpoint =
               new IPEndPoint(IPAddress.Parse("127.0.0.1"), port_udp_monitor);
            udp_listener = new UdpClient(udp_endpoint);
            redis = new RedisClient("127.0.0.1", 6379);
        }
        public void startServer()
        {
            t_udp_monitor = new Thread(udpMonitor);
            t_tcp_monitor = new Thread(tcpMonitor);
            tcp_listener = new TcpListener(port_tcp_monitor);
            try
            {
                server_state = true;
                t_udp_monitor.Start();
                t_tcp_monitor.Start();
                tcp_listener.Start();
                //receiveOver(CJLog.LogLevel.IMPORTANT_MSG, "服务已启动");
            }
            catch (Exception ex)
            {
                receiveOver(CJLog.LogLevel.FATAL_ERROR, "服务启动异常-" + ex.Message);
            }
        }
        public void stopServer()
        {
            try
            {
                tcp_listener.Stop();
            }
            catch (Exception ex)
            {
                receiveOver(CJLog.LogLevel.FATAL_ERROR, "服务终止异常-" + ex.Message);
            }
            try
            {
                t_tcp_monitor.Abort();
                server_state = false;
            }
            catch (Exception ex)
            {
                receiveOver(CJLog.LogLevel.FATAL_ERROR, "服务终止异常-"+ex.Message);
            }
            try
            {
                t_udp_monitor.Abort();
            }
            catch (Exception ex)
            {
                receiveOver(CJLog.LogLevel.FATAL_ERROR, "服务终止异常-" + ex.Message);
            }
            receiveOver(CJLog.LogLevel.IMPORTANT_MSG, "服务已停止");
        }

        public void receiveOver(string strXml) // 接收到数据后，触发事件 
        {
            if (ReceiveOver != null)
            {
                CJReceiveOverEvent e = new CJReceiveOverEvent();
                e.Type = 1;
                e.Msg = new CJMsg(strXml);
                ReceiveOver(this, e);
            }
        }
        public void receiveOver(CJLog.LogLevel level, string str) // 接收到数据后，触发事件 
        {
            if (ReceiveOver != null)
            {
                CJReceiveOverEvent e = new CJReceiveOverEvent();
                e.Level = level;
                e.Str = str;
                ReceiveOver(this, e);
            }
        }

        /**** Redis Start ****/
        public int db_addUser(string username, string password)
        {
            string uid = "";
            int max_id=10000;
            SortOptions sort = new SortOptions();
            sort.SortDesc = true;
            sort.Skip = 1;
            foreach (byte[] max_uid in redis.Sort("users", sort))
            {
                max_id=Encoding.UTF8.GetString(max_uid).ToInt();
            }
            max_id++;
            redis.SetEntryInHash(max_id.ToString(), "username", username);
            redis.SetEntryInHash(max_id.ToString(), "password", password);
            redis.RPush("users", Encoding.UTF8.GetBytes(max_id.ToString()));
            return max_id;
        }
        public void db_removeUser(string uid)
        {
            redis.RemoveItemFromList("users", uid.ToString());
        }
        public bool db_authUserPass(string uid, string password)
        {
            try
            {
                byte[] pass = redis.HGet(uid.ToString(),Encoding.UTF8.GetBytes("password"));
                if(Encoding.UTF8.GetString(pass) == password)
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                //receiveOver(0, "Redis验证用户时，出现异常-" + ex.Message);
            }
            return false;
        }
        public string db_getName(string uid)
        {
            string s = "";
            try
            {
                s = Encoding.UTF8.GetString(redis.HGet(uid, Encoding.UTF8.GetBytes("username")));
            }
            catch (Exception ex)
            {
                receiveOver(0, "Redis获取用户名时，出现异常-" + ex.Message);
            }
            return s;
        }
        public bool db_addFriend(string uid, string fuid)
        {
            try
            {
                redis.ZAdd(uid + "F", redis.ZCount(uid + "F",0,10000)+1, Encoding.UTF8.GetBytes(fuid));
                redis.ZAdd(fuid + "F", redis.ZCount(fuid + "F", 0, 10000) + 1, Encoding.UTF8.GetBytes(uid));
                return true;
            }
            catch (Exception ex)
            {
                receiveOver(CJLog.LogLevel.COMMON_ERROR, "Redis添加好友时，出现异常-"+ ex.Message);
            }
            return false ;
        }
        public List<user> db_getFriends(string uid)
        {
            CJUser u = new CJUser();
            foreach (byte[] b in redis.ZRange(uid+"F",0,10000))
	        {
                user utmp = new user();
                utmp.id = Encoding.UTF8.GetString(b);
                utmp.name = db_getName(utmp.id);
                u += utmp;
	        } 
            return u.Friends;
        }
        public void db_setOnline(string uid)
        {
            redis.SetEx(uid + "Online", online_timeout, Encoding.UTF8.GetBytes("true"));
        }
        public bool db_setOffline(string uid)
        {
            return redis.Remove(uid + "Online");
        }
        public bool db_isOnline(string uid)
        {
            if (redis.Exists(uid + "Online") > 0)
                return true;
            return false;
        }

        public void db_addMsgNoReceive(string uid, string msg)
        {
            redis.RPush(uid+"MsgNoReceive",Encoding.UTF8.GetBytes( msg));
        }
        public string db_popMsg_NoReceive(string uid) // 取出未接受的最早消息
        {
            string s ="";
            if (redis.LLen(uid + "MsgNoReceive") > 0)
            {
                byte[] b = redis.LPop(uid + "MsgNoReceive");
                s = Encoding.UTF8.GetString(b);
            }
            return s;
        }
        public List<string> db_popMsgs_NoReceive(string uid) // 取出未接受的所有消息
        {
            List<string> s = new List<string>();
            while (redis.LLen(uid + "MsgNoReceive") > 0)
            {
                s.Add(db_popMsg_NoReceive(uid));
            }
            return s;
        }
        public void db_addMsgRecord(string uid, string msg)
        {
            redis.RPush(uid + "Record", Encoding.UTF8.GetBytes(msg));
        }

        public long db_getMsgNoReceiveCount(string uid)
        {
            return redis.LLen(uid + "MsgNoReceive");
        }
        public long db_getMsgRecordsCount(string uid)
        {
            return redis.LLen(uid + "Record");
        }
        public string db_getMsgLastRecord(string uid)
        {
            string s = "";
            int count = (int)redis.LLen(uid + "Record");
            byte[][] b;
            if (count > 0)
            {
                b = redis.LRange(uid + "Record",count-1,count-1);
                s = Encoding.UTF8.GetString(b[0]);
            }
            return s;
        }
        public List<string> db_getMsgRecords(string uid)
        {
            List<string> s = new List<string>();
            int count = (int)redis.LLen(uid + "Record");
            byte[][] b;
            if (count > 0)
            {
                b = redis.LRange(uid + "Record", 0, count - 1);
                foreach (byte[] byt in b)
                {
                    s.Add(Encoding.UTF8.GetString(byt));
                }
            }
            return s;
        }
        public List<string> db_getMsgRecords(string uid,int num)    // 获取最近num条
        {
            List<string> s = new List<string>();
            int count = (int)redis.LLen(uid + "Record");
            byte[][] b;
            int start = (count - num - 1) > 0 ? (count - num - 1) : 0;
            if (count > 0)
            {
                b = redis.LRange(uid + "Record", start, count - 1);
                foreach (byte[] byt in b)
                {
                    s.Add(Encoding.UTF8.GetString(byt));
                }
            }
            return s;
        }
        /**** Redis Over ****/

        /**** Message Handel Start ****/
        private void tcpMonitor()
        {
            receiveOver(CJLog.LogLevel.IMPORTANT_MSG,
                string.Format("TCP监听正在启动，端口：{0}", port_tcp_monitor.ToString()));
            while (server_state)
            {
                try
                {
                    TcpClient client = tcp_listener.AcceptTcpClient();
                    Thread t_tcp_receive = new Thread(() => { tcpReceive(client); });
                    t_tcp_receive.Start();
                }
                catch (Exception ex)
                {
                    receiveOver(CJLog.LogLevel.FATAL_ERROR, "TCP建立连接出现异常-" + ex.Message);
                    return;
                }
            }
        }
        private void tcpReceive(TcpClient client)
        {
            while (server_state && client != null)
            {
                try
                {
                    int readLen = client.Available;
                    if (readLen > 0)
                    {
                        NetworkStream netStream = client.GetStream();
                        byte[] getData = new byte[2048];
                        netStream.Read(getData, 0, getData.Length);
                        string getMsg = Encoding.UTF8.GetString(getData);
                        tcpHandel(getMsg, netStream, client);
                    }
                }
                catch (Exception ex)
                {
                    receiveOver(CJLog.LogLevel.COMMON_ERROR, "TCP接收数据出现异常-" + ex.Message);
                    return;
                }
            }
        }
        private void tcpHandel(string strXml, NetworkStream netstream, TcpClient client)
        {
            CJMsg msg = new CJMsg();
            try
            {
                msg = new CJMsg(strXml);
            }
            catch (Exception ex)
            {
                receiveOver(CJLog.LogLevel.COMMON_ERROR, "TCP接收数据有误-" + ex.Message);
                return;
            }
            try
            {
                MsgType utype = msg.getMsgType();
                receiveOver(CJLog.LogLevel.COMMON_MSG,
                    string.Format("来自{0} 请求类型{1}",
                        client.Client.RemoteEndPoint.ToString(),
                        utype.ToString()));
                switch (utype)
                {
                    case MsgType.C_AUTH:
                        tcpMsgHandel_Auth(msg, netstream, client);
                        //tcpMsgHandel_Over(netstream,client); 
                        break;
                }
            }
            catch (Exception ex)
            {
                receiveOver(0, "TCP 发送数据出现异常-" + ex.Message);
            }
        }
        private void tcpMsgHandel_Auth(CJMsg msg, NetworkStream netstream, TcpClient client)
        {
            CJMsg.Peer upeer = msg.getMsgPeerFrom();
            string pass = msg.getMsgDataText();
            if (db_authUserPass(upeer.id, pass))
            {
                receiveOver(CJLog.LogLevel.COMMON_MSG,
                    string.Format("{0}{1} 登陆成功", upeer.id, db_getName(upeer.id)));
                CJMsg auth_msg = new CJMsg();
                auth_msg.setMsgType(MsgType.S_AUTH_RIGHT);
                auth_msg.setMsgFromNode("", port_tcp_monitor, "10000", db_getName("10000"));
                auth_msg.setMsgToNode(upeer.ip, upeer.port, upeer.id, db_getName(upeer.id));
                List<user> friends = db_getFriends(upeer.id);
                foreach (user u in friends)
                {
                    auth_msg.addMsgText_Node("id_name",u.id, u.name);
                } 
                foreach (string str in db_popMsgs_NoReceive(upeer.id))
                {
                    try
                    {
                        receiveOver(CJLog.LogLevel.DEBUG, str);
                        CJMsg tmp_msg;
                        tmp_msg= new CJMsg(str);
                        tmp_msg.formatByXml();
                        auth_msg.addMsgText_Node("no_receive", str);
                        db_addMsgRecord(tmp_msg.peer_from.id, str);
                        db_addMsgRecord(tmp_msg.peer_to.id, str);
                    }
                    catch (Exception ex)
                    {
                        receiveOver(CJLog.LogLevel.COMMON_ERROR, "Redis数据损坏-"+ex);
                    }
                }
                receiveOver(CJLog.LogLevel.DEBUG, auth_msg.ToString());
                byte[] sendData = new byte[2048];
                sendData = Encoding.Default.GetBytes(auth_msg.ToString());
                netstream.Write(sendData, 0, sendData.Length);
            }
            else
            {
                receiveOver(CJLog.LogLevel.COMMON_MSG,
                    string.Format("{0} 登陆失败", upeer.id));
                CJMsg auth_msg = new CJMsg();
                auth_msg.setMsgType(MsgType.S_AUTH_ERROR);
                auth_msg.setMsgFromNode("", port_tcp_monitor, "10000", db_getName("10000"));
                auth_msg.setMsgToNode(upeer.ip, upeer.port, upeer.id, db_getName(upeer.id));
                List<user> friends = db_getFriends(upeer.id);

                byte[] sendData = new byte[2048];
                sendData = Encoding.Default.GetBytes(auth_msg.ToString());
                netstream.Write(sendData, 0, sendData.Length);
            }
            netstream.Close();
            client.Close();
        }
        private void tcpMsgHandel_Over(NetworkStream netstream, TcpClient client)
        {
            CJMsg auth_msg = new CJMsg();
            auth_msg.setMsgType(MsgType.S_TCP_OVER);
            auth_msg.setMsgFromNode("", port_tcp_monitor, "10000", db_getName("10000"));
            byte[] sendData = new byte[2048];
            sendData = Encoding.Default.GetBytes(auth_msg.ToString());
            netstream.Write(sendData, 0, sendData.Length);

            netstream.Close();
            client.Close();
        }

        private void udpMonitor()
        {
            receiveOver(CJLog.LogLevel.IMPORTANT_MSG,
                string.Format("UDP监听正在启动，端口：{0}", port_udp_monitor.ToString()));
            while (server_state)
            {
                try
                {
                    byte[] b = udp_listener.Receive(ref udp_client_IPEnd);
                    string s = Encoding.UTF8.GetString(b);
                    Thread t_udp_handel = new Thread(() => { udpHandel(udp_client_IPEnd, s); });
                    t_udp_handel.Start();
                }
                catch (Exception)
                {
                    break;
                }
            }
        }
        private void udpHandel(IPEndPoint endpoing, string strXml)
        {
            CJMsg msg = new CJMsg();
            try
            {
                msg = new CJMsg(strXml);
            }
            catch (Exception ex)
            {
                receiveOver(CJLog.LogLevel.COMMON_ERROR, "UDP接收数据有误-" + ex.Message);
            }

            try
            {
                MsgType utype = msg.getMsgType();
                switch (utype)
                {
                    case MsgType.CS_CHAT_MSG: break;
                }
            }
            catch (Exception ex)
            {
                receiveOver(CJLog.LogLevel.COMMON_ERROR, "UDP接收数据出现异常-" + ex.Message);
                return;
            }
        }

        /**** Message Handel Over ****/
    }
}
