using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CHCJ_Server
{
    public struct user
    {
        public string id;
        public  string name;
    }
    public class CJUser
    {
        public CJUser()
        {
            user admin = new user();
            admin.id = "10000";
            admin.name = "admin";
            Friends = new List<user>();
            Friends.Add(admin);
        }
        string Id
        {
            get { return Id; }
            set { Id = value; }
        }
        string Name
        {
            get { return Name; }
            set { Name = value; }
        }
        public List<user> Friends;
        public static  CJUser operator +(CJUser a, user b)
        {
            a.Friends.Add(b);
            return a;
        }
        public static CJUser operator +(CJUser a ,CJUser b)
        {
            user u = new user();
            u.id = b.Id;
            u.name = b.Name;
            a.Friends.Add(u);
            return a;
        } 
    }
}
