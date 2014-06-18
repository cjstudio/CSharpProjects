using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CHCJ_Server
{
    public delegate void DelegateConnectHandle(object sender, CJReceiveOverEvent e);
    public delegate void DelegateLogHandle(object sender, CJLogEvent e);

    public class CJReceiveOverEvent : EventArgs
    {
        public CJReceiveOverEvent()
        { }

        private  CJMsg msg;
        public CJMsg Msg
        {
            get { return msg; }
            set { this.msg = value; }
        }
        private int e_type; // 0  为服务器系统消息
        public int Type
        {
            get { return e_type; }
            set { this.e_type = value; }
        }
        private string str;
        public string Str
        {
            get { return str; }
            set { this.str = value; }
        }
        private  CJLog.LogLevel level;
        public CJLog.LogLevel Level
        {
            get { return level; }
            set { this.level = value; }
        }
    }

    public class CJLogEvent : EventArgs
    {
        public CJLogEvent()
        { }

        private string _logstr;
        public string logStr
        {
            get { return _logstr; }
            set { this._logstr = value; }
        }
    }
}
