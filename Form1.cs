using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Security.AccessControl;
using System.Linq.Expressions;
using System.Data.SqlClient;
using System.IO;
using Microsoft.VisualBasic;


namespace JacobPopaBLOBLab8_Adv
{
    public partial class Form1 : Form
    {
        SqlConnection conn;
        private string CompleteFilePath = string.Empty;
        private string SavePath = string.Empty;
        public string getConnectionString()
        {
            return Net.Properties.Settings.Default.strConn;
        }
        private void GetCompleteFilePath()
        {
            OpenFileDialog OpenDialog = new OpenFileDialog();
            OpenDialog.Title = "Select Document File to Save";
            OpenDialog.ShowDialog();
            CompleteFilePath = OpenDialog.FileName;
        }
        private void CreateDocumentStorageTable()
        {
            var CreateTableCommand = new SqlCommand();
            CreateTableCommand.Connection = conn;
            CreateTableCommand.CommandType = CommandType.Text;
            CreateTableCommand.CommandText = "IF OBJECT_ID ( 'DocumentStorage' ) IS NOT NULL " +
            "DROP TABLE DocumentStorage; " + "CREATE TABLE DocumentStorage(" + "DocumentID int IDENTITY(1,1) NOT NULL, " + "FileName nvarchar(255) NOT NULL, " + "DocumentFile varbinary(max) NOT NULL)";
            CreateTableCommand.Connection.Open();
            CreateTableCommand.ExecuteNonQuery();
            CreateTableCommand.Connection.Close();
        }
        private void GetSavePath()
        {
            var SavePathDialog = new FolderBrowserDialog();
            SavePathDialog.Description = "Select a Folder to Restore BLOB file to";
            SavePathDialog.ShowDialog();
            SavePath = SavePathDialog.SelectedPath;

        }
        private void refreshBlobList()
        {
            var GetBlobListCommand = new SqlCommand("SELECT FileName FROM DocumentStorage",
            conn);
            SqlDataReader reader;
            GetBlobListCommand.Connection.Open();
            reader = GetBlobListCommand.ExecuteReader();
            while (reader.Read())
            {
                firstBlob.Items.Add(reader[0]);
            }
            reader.Close();
            GetBlobListCommand.Connection.Close();
            if (firstBlob.Items.Count < 0)
            {
                firstBlob.SelectedIndex = 0;
            }
        }
        private void SaveBlobToDatabase()
        {
            // This call lets you select the 
            // binary file to save As a BLOB
            // in the database.
            GetCompleteFilePath();
            // the BLOB holds the byte array to save.
            byte[] BLOB;
            // The FileStream is the stream of bytes
            // that represent the binary file.
            var FileStream = new FileStream(CompleteFilePath, FileMode.Open, FileAccess.Read);
            //The reader reads the binary data from the FileStream.
            var reader = new BinaryReader(FileStream);
            // the BLOB is assigned the bytes from the reader.
            // the file length is passed to the ReadBytes method
            // telling it how many bytes to read.
            FileInfo f = new FileInfo(CompleteFilePath);
            int i = (int)f.Length;
            BLOB = reader.ReadBytes(i);
            FileStream.Close();
            reader.Close();
            // Create a Command object to save
            // the BLOB value.
            var SaveDocCommand = new SqlCommand();
            SaveDocCommand.Connection = conn;
            SaveDocCommand.CommandText = "INSERT INTO DocumentStorage" + "(FileName, DocumentFile)" + "VALUES (@FileName, @DocumentFile)";
            //Create parameter to store the filename and BLOB Data.
            var FileNameParameter = new SqlParameter("@FileName", SqlDbType.NChar);
            var DocumentFileParameter = new SqlParameter("@DocumentFile", SqlDbType.Binary);
            SaveDocCommand.Parameters.Add(FileNameParameter);
            SaveDocCommand.Parameters.Add(DocumentFileParameter);
            // Parse the filename out of the complete path
            // and assign it to the parameter.
            FileNameParameter.Value = CompleteFilePath.Substring(CompleteFilePath.LastIndexOf(@"\") + 1);
            // Set the DocumentFile parameter to the BLOB Value.
            DocumentFileParameter.Value = BLOB;
            // Execute the command and save the BLOB to the database.
            try
            {
                SaveDocCommand.Connection.Open();
                SaveDocCommand.ExecuteNonQuery();
                MessageBox.Show(FileNameParameter.Value.ToString() + "Saved to DataBase.",
                "BLOB Saved!", MessageBoxButtons.OK, MessageBoxIcon.Information);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Save Failed", MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            }
            finally
            {
                SaveDocCommand.Connection.Close();
            }
        }
        private void FetchBlobFromDatabase()
        {
            // Verify there is BLOB selected to retrieve.
            if (firstBlob.Text == "")
            {
                MessageBox.Show("Select a BLOB to fetch from the ComboBox");
                return;
            }
            // Get the path to save the BLOB to.
            GetSavePath();
            // Create the Command object to fetch the slected BLOB.
            var GetBlobCommand = new SqlCommand("SELECT FileName, DocumentFile " +
                "FROM DocumentStorage " + "WHERE FileName = @DocName", conn);
            GetBlobCommand.Parameters.Add("@DocName", SqlDbType.NVarChar).Value = firstBlob.Text;
            // Current index to write the bytes to 
            long CurrentIndex = 0;
            // number of bytes to store in the BLOB.
            int BufferSize = 100;
            //Actual number of bytes returned when calling GetBytes.
            long BytesReturned;
            // The Byte array used to hold the buffer.
            var Blob = new byte[BufferSize - 1 + 1];
            GetBlobCommand.Connection.Open();
            SqlDataReader reader = GetBlobCommand.ExecuteReader(CommandBehavior.SequentialAccess);
            while (reader.Read())
            {
                // Create or open the selected file.
                var FileStream = new FileStream(SavePath + @"\" + reader["FileName"].ToString(), FileMode.OpenOrCreate, FileAccess.Write);
                // Set the writer to write the BLOB to the file.
                var writer = new BinaryWriter(FileStream);
                // Reset the index to the beginning of the file.
                CurrentIndex = 0;
                // Set the BytesReturned to the actual number of bytes returned by the GetBytes call.
                BytesReturned = reader.GetBytes(1, CurrentIndex, Blob, 0, BufferSize);
                // If the BytesReturned fills the buffer keep appending to the file.
                while (BytesReturned == BufferSize)
                {
                    writer.Write(Blob);
                    writer.Flush();
                    CurrentIndex += BufferSize;
                    BytesReturned = reader.GetBytes(1, CurrentIndex, Blob, 0, BufferSize);
                }
                // When the BytesReturned no longer fills the buffer, write the remaining bytes.
                writer.Write(Blob, 0, (int)(BytesReturned - 1));
                writer.Flush();
                writer.Close();
                FileStream.Close();
            }
            reader.Close();
            GetBlobCommand.Connection.Close();
        }

        public Form1()
        {
            InitializeComponent();
        }
        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void btnCreateDatabase_Click(object sender, EventArgs e)
        {
            conn = new SqlConnection(getConnectionString());
            DialogResult response = MessageBox.Show("Create the Document Storage Table?" +
            Environment.NewLine + "Click Yes to create a new Document Storage Table. Click no if you " +
            "already have one!", "Create Document Storage Table", MessageBoxButtons.YesNo,
            MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            switch (response)
            {
                case DialogResult.Yes:
                    {
                        CreateDocumentStorageTable();
                        break;
                    }
                case DialogResult.No:
                    {
                        refreshBlobList();
                        break;
                    }
            }

        }

        private void btnRefreshList_Click_1(object sender, EventArgs e)
        {
            refreshBlobList();
        }

        private void btnSaveBlob_Click_1(object sender, EventArgs e)
        {
            SaveBlobToDatabase();
            refreshBlobList();
        }

        private void btnFetchBlob_Click_1(object sender, EventArgs e)
        {
            FetchBlobFromDatabase();
        }
    }
}

