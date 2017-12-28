using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TTI.Ford.GPP.FTPBOMDataImport
{
    /// <summary>
    /// only one instance of this class
    /// </summary>
    public class Global
    {
       // public static readonly object _lock = new object();
      // public static int CurrentImportLogID { get; set; }


        private static volatile Global instance;
        private static object syncRoot = new Object();
        private static string logFile;
        private Global() {

            currentImportDetail = new ImportDetail();
            previousImportDetail = new ImportDetail();
            logFile = DateTime.Now.ToString("yyyy_MM_dd_") + "FTPBOMDataImport.log";
        }
        
        public static Global Instance
        {
            get 
            {
                if (instance == null) 
                {
                    lock (syncRoot) 
                    {
                        if (instance == null)
                            instance = new Global();
                    }
                }

               return instance;
            }
        }


        private ImportDetail currentImportDetail; // current import detail
        private ImportDetail previousImportDetail; //detail from last import
        /// <summary>
        /// Initialize and reset global data
        /// </summary>
        public void InitializeGlobals()
        {
            currentImportDetail = new ImportDetail();
            previousImportDetail = new ImportDetail();
            
        }

        public ImportDetail CurrentDetail { get { return currentImportDetail; } }
        public ImportDetail PreviousDetail { get { return previousImportDetail; } }
        public string LogFile { get { return logFile; } }
    }

    /// <summary>
    /// import detail
    /// </summary>
    public class ImportDetail
    {
       public int ImportLogID; //hold current id, if row created
       public string ImportFileName;
       public string ChecksumFileName;
       public string FileSequence;
       public string FileDownloadLocation;
       public int BOMUploadBatchID;
       public string LogDescription;
       public bool FileDownloadCompleted;
       public bool ChecksumFileDownloadCompleted;
       public bool FileCheckSumSuccessful;
       public bool BOMUploadProcessSuccessful;// BOM upload sucessful with no critical errors
       public bool BOMUploadProcessInprogress;//after downloading file processing starts

       public ImportDetail()
       {
            ImportLogID = 0;
            ImportFileName = string.Empty;
            ChecksumFileName = string.Empty;
            FileSequence = string.Empty;
            FileDownloadLocation = string.Empty;
            BOMUploadBatchID = 0;
            LogDescription = string.Empty;
            FileDownloadCompleted = false;
            ChecksumFileDownloadCompleted = false;
            FileCheckSumSuccessful = false;
            BOMUploadProcessSuccessful = false;// BOM upload sucessful with no critical errors
            BOMUploadProcessInprogress = false;

       }
               
    }
    /// <summary>
    /// file and checksum related info
    /// </summary>
    public class FileGroup
    {
        public string Sequence;
        public string Filename;
        public string ChecksumFilename;
        public string FileDate;
        public string ChecksumFileDate;
        public string UriFileSuffix;
        public string UriChecksumSuffix;
        public bool IsDaily;

        public FileGroup()
        {
            Sequence = string.Empty;
            Filename = string.Empty;
            ChecksumFilename = string.Empty;
            FileDate = string.Empty;
            ChecksumFileDate = string.Empty;
            UriFileSuffix = string.Empty;
            UriChecksumSuffix = string.Empty;
            IsDaily = false;
        }
    }

   
}
