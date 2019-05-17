using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using System.Windows.Forms;

namespace MSSqlServerListener
{


    public enum QuerryType 
    {
        /// <summary>
        /// Strored procedure Type
        /// </summary>
        P,
        /// <summary>
        /// Querry Type
        /// </summary>
        Q
    }


    public class MSSqlListener
    {
        #region 'private members'
        private List<String> ReservedSqlSymbols;
        private List<String> ParseSymbols;
        private String querry = "";
        
        private SqlConnection cn;
        private SqlConnectionStringBuilder str;
        private Action callback;
        private QuerryType typeQuerry;
        private bool runnig = false;

        private String spName;
        private String querryText;
        #endregion

        #region 'Public members'

        /// <summary>
        /// Stored Procedure Name, example dbo.SPName
        /// </summary>
        public String SpName { get { return spName; } set { spName = value; } }

        /// <summary>
        /// Querry Text
        /// </summary>
        public String QuerryText { get { return querryText; } set { querryText = value; } }

        #endregion

        private enum querryType
        {
           AF     //  агрегатная функция (среда CLR)  
           ,C     //  ограничение CHECK  
           ,D     //  ограничение по умолчанию или DEFAULT 
           ,F     //   ограничение FOREIGN KEY
           ,FN    //  скалярная функция    
           ,FS    //  скалярная функция сборки (среда CLR)   
           ,FT    //   функция сборки с табличным значением (среда CLR) IF = подставляемая табличная функция 
           ,IT    //   внутренняя таблица   
           ,K     //  ограничение PRIMARY KEY или UNIQUE
           ,L     // журнал   
           ,P     //   хранимая процедура 
           ,PC    //   хранимая процедура сборки (среда CLR)    
           ,R     //правило           
           ,RF    // хранимая процедура фильтра репликации           
           ,S     //системная таблица           
           ,SN    //синоним           
           ,SQ    //очередь обслуживания           
           ,TA    //триггер DML сборки (среда CLR)           
           ,TF    //табличная функция           
           ,TR    //триггер DML SQL           
           ,TT    //табличный тип           
           ,U     //пользовательская таблица           
           ,V     //представление           
           ,X     //расширенная хранимая процедура
        };

        
        #region 'Constructors'
        public MSSqlListener(String ConnStr, QuerryType t, Action Callback)
        {
            cn = new SqlConnection(ConnStr);
            str = new SqlConnectionStringBuilder(ConnStr);
            SqlDependency.Stop(ConnStr);
            SqlDependency.Start(ConnStr);

            typeQuerry = t;
            callback = Callback;
            ReservedSqlSymbols = DefaultSqlSymbols();
            ParseSymbols = DefaultSqlParseSymbols();
        }

        public MSSqlListener(String Querry, String ConnStr, QuerryType t, Action Callback)
        {
            cn = new SqlConnection(ConnStr);
            str = new SqlConnectionStringBuilder(ConnStr);
            SqlDependency.Stop(ConnStr);
            SqlDependency.Start(ConnStr);

            typeQuerry = t;
            querry = Querry;
            callback = Callback;
            ReservedSqlSymbols = DefaultSqlSymbols();
            ParseSymbols = DefaultSqlParseSymbols();
        }
        public MSSqlListener(String Querry, SqlConnectionStringBuilder scsb, Action Callback)
        {
            cn = new SqlConnection(scsb.ConnectionString);
            SqlDependency.Stop(scsb.ConnectionString);
            SqlDependency.Start(scsb.ConnectionString);


            querry = Querry;
            str = scsb;
            callback = Callback;
            ReservedSqlSymbols = DefaultSqlSymbols();
            ParseSymbols = DefaultSqlParseSymbols();
        }

        #endregion

        #region 'StartStopFunc'
        /// <summary>
        /// Starting MSSqlListener
        /// </summary>
        public void Start()
        {
            runnig = true;
            ExecuteWatchingQuery();
        }

        /// <summary>
        /// Stopping MSSqlListener
        /// </summary>
        public void Stop()
        {
            runnig = false;
        }
        #endregion

        #region 'DataBase Func'
        private void ExecuteWatchingQuery()
        {
            if (runnig)
            {
                using (SqlConnection connection = new SqlConnection(str.ConnectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand())
                    {
                        command.Connection = connection;
                        command.CommandText = this.GetCommandText();
                        var sqlDependency = new SqlDependency(command);
                        sqlDependency.OnChange += new OnChangeEventHandler(OnDatabaseChange);
                        command.ExecuteReader();
                    }
                }
            }
        }

        private void OnDatabaseChange(object sender, SqlNotificationEventArgs args)
        {
            SqlNotificationInfo info = args.Info;
            if (SqlNotificationInfo.Insert.Equals(info)
                || SqlNotificationInfo.Update.Equals(info)
                || SqlNotificationInfo.Delete.Equals(info))
            {
                this.callback();
            }
            ExecuteWatchingQuery();
        }

        #endregion

        #region 'TextFunctions'

        private List<String> DefaultSqlSymbols()
        {
            List<String> Symbols = new List<String>();
            Symbols.Add("CREATE");
            Symbols.Add("ALTER");
            //Symbols.Add("AS");
            Symbols.Add("BEGIN");
            Symbols.Add("END");
            Symbols.Add("SET");
            Symbols.Add("NOCOUND");

            return Symbols;
        }
        private List<String> DefaultSqlParseSymbols()
        {
            List<String> Symbols = new List<String>();
            Symbols.Add("/*");
            Symbols.Add("*/");
            Symbols.Add("--");

            return Symbols;
        }

        private String GetCommandText()
        {
            String text = "";

            switch (typeQuerry)
            {
                case QuerryType.P: text = GetProcedureText(); break;
                case QuerryType.Q: text = querryText; break;
                default: text = ""; break;
            }

            return text;
        }
 


        private String GetProcedureText()
        {
            SqlCommand cmd = new SqlCommand();
            cmd.Connection = cn;
            cmd.CommandText = @"SELECT 
            'SELECT ' + ISNULL(referenced_minor_name,'') + ' FROM ' + referenced_schema_name +'.'+referenced_entity_name + '   ' AS querryListener--,
	        /*referenced_schema_name, referenced_entity_name, referenced_minor_name,   
            referenced_minor_id, referenced_class_desc, is_caller_dependent, is_ambiguous  */
            FROM sys.dm_sql_referenced_entities ('" + spName+ @"', 'OBJECT') WHERE referenced_minor_name IS NOT NULL";

            SqlDataReader reader;
            List<String> res = new List<string>();
            try
            {
                cn.Open();
                reader = cmd.ExecuteReader();

                if (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        res.Add(reader["querryListener"].ToString());
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
            finally { cn.Close(); }

            var a = res.Where(x => ReservedSqlSymbols.All(rr => !(x.Contains(rr)))).ToList<String>();
            List<String> result = new List<string>();

            bool f = false;
            foreach(String tmp in a)
            {
                    if (ParseSymbols.Any(tmp.Contains))
                    {
                        String separator = ParseSymbols.Find(tmp.Contains);
                        switch (separator)
                        {
                            case "--":
                                result.Add(tmp.Substring(0, tmp.Length - tmp.IndexOf(separator)));
                                break;
                            case "/*":
                                result.Add(tmp.Substring(0, 0 + tmp.IndexOf(separator))); if(tmp.IndexOf("*/") < 0) {f = true;}
                                break;
                            case "*/":
                                result.Add(tmp.Substring(tmp.Trim().IndexOf(separator) + 2 , tmp.Trim().Length - tmp.Trim().IndexOf(separator))); if(tmp.IndexOf("/*") < 0) {f = false;};
                                break;
                            default:
                                result.Add(tmp);
                                break;
                        }
                    }
                    else { if (!f) { result.Add(tmp); } }           
            }

            return String.Concat(result.ToArray()).Trim();
        }
        #endregion
    }
}
