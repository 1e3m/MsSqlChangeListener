using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Data.SqlClient;


/*Отслеживание изменений в таблицах БД*/
using MSSqlServerListener;

namespace GetNotification
{
    public partial class Form1 : Form
    {
        SqlConnectionStringBuilder str;
        SqlConnection cn;

        MSSqlListener Listener;

        public Form1()
        {
            InitializeComponent();
            str = new SqlConnectionStringBuilder();
            str.ConnectionString = ""; 
            cn = new SqlConnection(str.ConnectionString);

            Listener = new MSSqlListener(str.ConnectionString, MSSqlServerListener.QuerryType.Q, this.Fill);           
            CheckConnection();
            Fill();
        }

        private void CheckConnection()
        {
            try
            {
                cn.Open();
                MessageBox.Show("Connection OK");
            }
            catch (Exception ex) { MessageBox.Show("Connection FAILED"); }
            finally { cn.Close(); }
        }

        private void Fill()
        {
            SqlCommand cmd = new SqlCommand();
            cmd.Connection = cn;
            //cmd.CommandText = "SELECT nId, cValue FROM dbo.TestNotification where nId < 5";
            cmd.CommandText = "exec dbo.getAllT";

            /*Запуск отлеживания изменений*/

            Listener.SpName = "dbo.getAllT";
            Listener.QuerryText = "select nId FROM dbo.TestNotification";
            Listener.Start();
            SqlDataReader reader;// = new SqlDataReader();
            try
            {
                cn.Open();
                reader = cmd.ExecuteReader();
                if(reader.HasRows)
                {
                    DataTable tbl = new DataTable();
                    tbl.Load(reader);
                    if (this.dataGridView1.IsHandleCreated)
                    {
                        this.dataGridView1.BeginInvoke((MethodInvoker)(delegate { this.dataGridView1.DataSource = tbl; }));
                    }
                    else
                    {
                        this.dataGridView1.DataSource = tbl;
                    }
                    //dataGridView1.DataSource =  tbl;
                }
                cn.Close();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message.ToString()); }
            finally { cn.Close(); }
        }
        
    }
    
}
