using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TTI.Ford.GPP.FTPBOMDataImport
{
    public class ImportSession
    {
        Global global;
        public ImportSession()
        {
            //get current instance 
            global = Global.Instance;
            //must initialize globals
            global.InitializeGlobals();
        }

        
        public  void RunSession()
        {

            Utility.WriteLog("-------------Session started--------------");
            Console.WriteLine("Session Started");
            ////instantiate 
            //Global global = Global.Instance;
            ////must initialize globals
            //global.InitializeGlobals();
            ImportDetail current = global.CurrentDetail;
            ImportDetail previous = global.PreviousDetail;
            string outMsg = string.Empty;
            string msg = string.Empty;
            bool returnOK = false;
            //get previous sequence and file name from db

            if (!DBService.LoadPreviousImportDetail(out outMsg))
            {
                Utility.WriteLog(string.Format("Unable to get previous upload details. Previous import data load error: {0} ", outMsg));
                return;
            }

            msg = string.Format("Previous import log id: {0}, import file: {1}, checksum file: {2}  ", previous.ImportLogID, previous.ImportFileName, previous.ChecksumFileName);
            Utility.WriteLog(msg);

            //check if previous download was complete
            //if downloads not complete, current and prvious will be same because for daily, hourly proceed to next
            if (!previous.FileDownloadCompleted || !previous.ChecksumFileDownloadCompleted)
            {
                //If file is hourly continue
                //If file is daily e-mail for resolution -- do not continue
                if (Utility.IsSequenceDaily(previous.FileSequence))
                {
                    msg = string.Format("File not downloded successful in previous run. Import log id: {0}, file:  {1}, checksum file: {2} ", previous.ImportLogID, previous.ImportFileName, previous.ChecksumFileName);
                    Utility.WriteLog(msg);
                    EMailer.SendEmail(msg);
                    return;
                }

            }

            //if previous download was successful but checksum issue
            if (!previous.FileCheckSumSuccessful)
            {
                //If file is hourly continue
                //If file is daily e-mail for resolution -- do not continue
                msg = string.Format("Previous bom upload checksum failed for file {0}, check log id:  {1} ", previous.ImportFileName, previous.ImportLogID);
                if (Utility.IsSequenceDaily(previous.FileSequence))
                {
                    Utility.WriteLog(msg);
                    EMailer.SendEmail(msg);
                    return;
                }

            }
            // previous upload not successful
            if (!previous.BOMUploadProcessSuccessful)
            {
                //If file is hourly continue
                //If file is daily e-mail for resolution -- do not continue
                if (Utility.IsSequenceDaily(previous.FileSequence))
                {
                    msg = string.Format("Previous bom upload did not completed successfully for file {0}, log id:  {1}, BOM batch id: {2} ", previous.ImportFileName, previous.ImportLogID, previous.BOMUploadBatchID);
                    Utility.WriteLog(msg);
                    EMailer.SendEmail(msg);
                    return;
                }

            }

            //get list of  avaible files on ftp server
            Utility.WriteLog(string.Format("Getting file list for processing "));
            SortedList<string, FileGroup> fileListSorted;

            returnOK = FTPService.GetFileListSorted(out fileListSorted, out outMsg);
            if (!returnOK)
            {
                Utility.WriteLog(outMsg);
                return;
            }

            Utility.WriteLog("Files count: " + fileListSorted.Count);
            if (fileListSorted.Count == 0)
            {
                Utility.WriteLog("No files available for processing at this time.");
                return;
            }


            //determine next file for processing. If next file is hourly and doesn't exist,find next
            //if next file is daily and doesn't exist, send email, no futher processing until resolution
            string nextSequence = Utility.GetNextAvailableSequence(previous.FileSequence, fileListSorted);
            Utility.WriteLog(string.Format("Next expected sequence for processing: {0} ", nextSequence));

            if (fileListSorted.ContainsKey(nextSequence))
            {
                current.FileSequence = nextSequence;
                current.ImportFileName = fileListSorted[nextSequence].Filename;
                current.ChecksumFileName = fileListSorted[nextSequence].ChecksumFilename;

            }
            else
            {//daily not available
                msg = string.Format("Next file with sequence expected to be processed not found: {0} ", nextSequence);
                Utility.WriteLog(msg);
                //send e-mail
                EMailer.SendEmail(msg);
                return;
            }

            //daily file
            if (fileListSorted.ContainsKey(nextSequence))
            {
                //check both files exist
                if (String.IsNullOrEmpty(fileListSorted[nextSequence].Filename) && Utility.IsSequenceDaily(nextSequence))
                {
                    //missing File
                    msg = string.Format("Expected file with sequence {0} not found.", nextSequence);
                    Utility.WriteLog(msg);
                    EMailer.SendEmail(msg);
                    return;
                }
                if (String.IsNullOrEmpty(fileListSorted[nextSequence].ChecksumFilename) && Utility.IsSequenceDaily(nextSequence))
                {
                    //missing checksum File
                    msg = string.Format("Expected checksum file with sequence {0} not found.", nextSequence);
                    Utility.WriteLog(msg);
                    EMailer.SendEmail(msg);
                    return;
                }
            }

            current.FileDownloadLocation = ConfigHelper.DownloadDir;
            //log record
            current.LogDescription = "About to download file: " + fileListSorted[nextSequence].Filename;
            DBService.LogDataImportProcessing(current);
            //download file
            bool downloadOk = FTPService.DownloadImportFile(fileListSorted[nextSequence], false);

            if (!downloadOk)
            {
                //issue log
                msg = string.Format("Error downloading file: {0}", fileListSorted[nextSequence].Filename);
                current.LogDescription = msg;
                Utility.WriteLog(msg);
                if (Utility.IsSequenceDaily(nextSequence))
                {
                    EMailer.SendEmail(msg);
                }
                DBService.LogDataImportProcessing(current);
                return;
            }
            msg = string.Format("File download successful: {0}", fileListSorted[nextSequence].Filename);
            current.LogDescription = msg;
            current.FileDownloadCompleted = true;
            Utility.WriteLog(msg);
            DBService.LogDataImportProcessing(current);
            //download checksum

            downloadOk = FTPService.DownloadImportFile(fileListSorted[nextSequence], true);

            if (!downloadOk)
            {
                msg = string.Format("Error downloading checksum file: {0}", fileListSorted[nextSequence].Filename);
                current.LogDescription = msg;
                Utility.WriteLog(msg);
                if (Utility.IsSequenceDaily(nextSequence))
                {
                    EMailer.SendEmail(msg);
                }
                //issue log
                DBService.LogDataImportProcessing(current);
                return;
            }

            msg = string.Format("ChecksumFile download successful: {0}", fileListSorted[nextSequence].ChecksumFilename);
            current.LogDescription = msg;
            current.ChecksumFileDownloadCompleted = true;
            DBService.LogDataImportProcessing(current);
            Utility.WriteLog(msg);

            string downloadChk = Utility.GetSha256(fileListSorted[nextSequence].Filename);

            string givenChk = Utility.ReadChecksumFrom(fileListSorted[nextSequence].ChecksumFilename);

            if (givenChk.CompareTo(downloadChk) != 0)
            {
                msg = string.Format("Checksum did not match for file {0}, given: {1}  calculated: {2} ", current.ImportFileName, givenChk, downloadChk);
                current.LogDescription = msg;
                Utility.WriteLog(msg);
                current.FileCheckSumSuccessful = false;
                DBService.LogDataImportProcessing(current);
                if (Utility.IsSequenceDaily(nextSequence))
                {
                    EMailer.SendEmail(msg);
                }
                return;
            }

            msg = string.Format("checksum successful, given: {0} calculated: {1} ", givenChk, downloadChk);
            current.LogDescription = msg;
            current.FileCheckSumSuccessful = true;
            DBService.LogDataImportProcessing(current);
            Utility.WriteLog(msg);

            //get next batchid
            int batchID;
            DBService.GetNextBOMUploadBatchIDForImport(current.FileDownloadLocation, current.ImportFileName, out batchID);
            if (batchID <= 0)
            {
                msg = string.Format("Error getting BOM upload batch id: {0}, cannot upload BOM import ", batchID);
                current.LogDescription = msg;
                Utility.WriteLog(msg);
                DBService.LogDataImportProcessing(current);
                if (Utility.IsSequenceDaily(nextSequence))
                {
                    EMailer.SendEmail(msg);
                }
                return;
            }
            msg = string.Format("BOM batch id for upload processing: {0} ", batchID);
            current.BOMUploadBatchID = batchID;
            current.LogDescription = msg;
            current.BOMUploadProcessInprogress = true;
            DBService.LogDataImportProcessing(current);
            Utility.WriteLog(msg);
            int status;
            string processMsg;
            string errMsg;
            DBService.ProcessBOMUpload(batchID, out status, out processMsg, out errMsg);
            //check status
            current.BOMUploadProcessInprogress = false;
            if (status != 0 && processMsg != "Success") //Initial request for BOMUploadBatchID

            //if (status != 0 && processMsg != "Initial request for BOMUploadBatchID")
            {
                msg = string.Format("BOM upload process for import file {0}, encounter issues. Please check batch id: {1} Description : {2} ", current.ImportFileName, batchID, processMsg);
                current.LogDescription = msg;
                Utility.WriteLog(msg);
                if (Utility.IsSequenceDaily(nextSequence))
                {
                    EMailer.SendEmail(msg);
                }
                //issue log
                DBService.LogDataImportProcessing(current);
                return;

            }
            //upload completed
            current.BOMUploadProcessSuccessful = true;
            msg = string.Format("BOM upload process completed for file {0}. Completion Status : {1} ", current.ImportFileName, processMsg);
            current.LogDescription = msg;
            DBService.LogDataImportProcessing(current);
            Utility.WriteLog(msg);

            //done

        } //run session

    }
}
