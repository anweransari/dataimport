using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data.SqlClient;
using System.Data;
using System.Diagnostics;

namespace TTI.Ford.GPP.FTPBOMDataImport
{
    public class DBService
    {
        private static object _lock = new object();
        public static int criticalValue = 10;

        public static int ExecuteNonQuery(string query, List<SqlParameter> parameters, int cmdTimeOut, out string errorMsg)
        {
            int iReturn = 0;
            errorMsg = string.Empty;
            SqlConnection conn = OpenDB();
            SqlCommand cmd = new SqlCommand(query, conn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandTimeout = cmdTimeOut;

            if (parameters != null && parameters.Count > 0)
                cmd.Parameters.AddRange(parameters.ToArray());
            try
            {
                iReturn = cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Error Executing Query:{0}.  Exception{1}", query, ex.Message));
                errorMsg = ex.Message;
            }
            finally
            {
                cmd.Dispose();

                if (conn.State != ConnectionState.Closed)
                    conn.Close();//cleaning up.
            }

            return iReturn;
        }

        /// <summary>
        /// returns command execution status to the caller with single out param 
        /// </summary>
        /// <param name="query"></param>
        /// <param name="parameters"></param>
        /// <param name="cmdTimeOut"></param>
        /// <param name="outParamName">Name of out parameter of int return type</param>
        /// <returns></returns>
        public static CmdReturnStatus ExecuteNonQueryOutParam(string query, List<SqlParameter> parameters, int cmdTimeOut, string outParam1, string outParam2)
        {

            CmdReturnStatus returnStatus = new CmdReturnStatus();
            returnStatus.errorMsg = string.Empty;
            returnStatus.error = false;
            SqlConnection conn = OpenDB();
            SqlCommand cmd = new SqlCommand(query, conn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandTimeout = cmdTimeOut;

            if (parameters != null && parameters.Count > 0)
            {
                cmd.Parameters.AddRange(parameters.ToArray());
            }

            try
            {
                returnStatus.returnValue = cmd.ExecuteNonQuery();
                if (outParam1 != string.Empty)
                {
                    returnStatus.outParam1 = cmd.Parameters[outParam1];
                }

                if (outParam2 != string.Empty)
                {
                    returnStatus.outParam2 = cmd.Parameters[outParam2];
                }
            }
            catch (Exception ex)
            {
                //Debug.WriteLine(string.Format("Error Executing Query:{0}.  Exception{1}", query, ex.Message));
                returnStatus.errorMsg = ex.Message;
                returnStatus.error = true;
            }
            finally
            {
                cmd.Dispose();

                if (conn.State != ConnectionState.Closed)
                    conn.Close();//cleaning up.
            }

            return returnStatus;
        }

        

        //if there is an error, send email.
        /// <summary>
        /// Log processing info
        /// </summary>
        /// <returns>return false if error executing command</returns>
        public static bool LogDataImportProcessing(ImportDetail logData) //need values in param from caller
        {

            string sp = "LogFTPBOMDataImport";
            CmdReturnStatus cmdStatus;
            List<SqlParameter> parameters = new List<SqlParameter>();
            int cmdTimeOut = ConfigHelper.ShortQueryTimeout;
            //int iReturn = 0;
            //string errorMsg = string.Empty;

            SqlParameter param;
            if (logData.ImportLogID > 0)
            {
                param = new SqlParameter("@ID", SqlDbType.Int);
                param.Value = logData.ImportLogID;
                parameters.Add(param);
            }

        
            param = new SqlParameter("@ImportFileName", SqlDbType.VarChar, 100);
            param.Value = logData.ImportFileName;
            parameters.Add(param);

            param = new SqlParameter("@ChecksumFileName", SqlDbType.VarChar, 100);
            param.Value = logData.ImportFileName;
            parameters.Add(param);

            param = new SqlParameter("@FileSequence", SqlDbType.VarChar, 7);
            param.Value = logData.FileSequence;
            parameters.Add(param);

            param = new SqlParameter("@FileDownloadLocation", SqlDbType.VarChar, 500);
            param.Value = logData.FileDownloadLocation;
            parameters.Add(param);

            param = new SqlParameter("@LogDescription", SqlDbType.VarChar, 500);
            param.Value = logData.LogDescription;
            parameters.Add(param);

            param = new SqlParameter("@FileDownloadCompleted", SqlDbType.Bit, 1);
            param.Value = logData.FileDownloadCompleted;
            parameters.Add(param);

            param = new SqlParameter("@ChecksumFileDownloadCompleted", SqlDbType.Bit, 1);
            param.Value = logData.ChecksumFileDownloadCompleted;
            parameters.Add(param);

            param = new SqlParameter("@FileCheckSumSuccessful", SqlDbType.Bit, 1);
            param.Value = logData.FileCheckSumSuccessful;
            parameters.Add(param);

            param = new SqlParameter("@BOMUploadBatchID", SqlDbType.Int, 4);
            param.Value = logData.BOMUploadBatchID;
            parameters.Add(param);

            param = new SqlParameter("@BOMUploadProcessInprogress",  SqlDbType.Bit, 1);
            param.Value = logData.BOMUploadProcessInprogress;
            parameters.Add(param);

            param = new SqlParameter("@BOMUploadProcessSuccessful",  SqlDbType.Bit, 1);
            param.Value = logData.BOMUploadProcessSuccessful;
            parameters.Add(param);

            param = new SqlParameter("@IDOut", SqlDbType.Int, 4);
            param.Direction = ParameterDirection.Output;
            parameters.Add(param);
            //use ID param for updates if update fille @ID

            cmdStatus = DBService.ExecuteNonQueryOutParam(sp, parameters, cmdTimeOut, "@IDOut",string.Empty);
            if (cmdStatus.error)
            {
                return false;
            }
            int outID = (int)cmdStatus.outParam1.Value;

            if (outID > 0)
                logData.ImportLogID = outID;

            return true;

            

        }






        /// <summary>
        /// This will be called before current import begins. At this time we don't have 
        /// current import log entry in the log table
        /// </summary>
        /// <param name="updateRow"></param>
        public static bool LoadPreviousImportDetail(out string msg)
        {
            string sp = "getLastFTPBOMDataImportInfo";

            //int iReturn = 0;
            msg = string.Empty;
            SqlConnection conn = null;

            //previous import details needs to be loaded before current download begins
            if (Global.Instance.PreviousDetail.FileSequence != string.Empty && Global.Instance.PreviousDetail.ImportLogID > 0)
            {
                 return true; //already loaded
            }


            try
            {
                conn = OpenDB();
                SqlCommand cmd = new SqlCommand(sp, conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = ConfigHelper.ShortQueryTimeout;
                using (SqlDataReader reader = cmd.ExecuteReader())
                {

                    if (reader.HasRows)
                    {
                        
                        if (reader.Read()) // one rec expected
                        {

                            ImportDetail prvDetail = Global.Instance.PreviousDetail;
                            prvDetail.ImportLogID = (int)reader["ID"];
                            prvDetail.ImportFileName = reader["ImportFileName"].ToString();
                           
                            prvDetail.ChecksumFileName = reader["ChecksumFileName"].ToString();
                            prvDetail.FileSequence = reader["FileSequence"].ToString();
                            prvDetail.FileDownloadLocation = reader["FileDownloadLocation"].ToString();
                            prvDetail.BOMUploadBatchID = (int)reader["BOMUploadBatchID"];  

                            prvDetail.LogDescription = reader["LogDescription"].ToString();
                            prvDetail.FileDownloadCompleted = (bool) reader["FileDownloadCompleted"];
                            prvDetail.ChecksumFileDownloadCompleted = (bool) reader["ChecksumFileDownloadCompleted"];
                            prvDetail.FileCheckSumSuccessful = (bool)reader["FileCheckSumSuccessful"];
                            prvDetail.BOMUploadProcessInprogress = (bool) reader["BOMUploadProcessInprogress"];
                            prvDetail.BOMUploadProcessSuccessful = (bool)reader["BOMUploadProcessSuccessful"];
                          
                        }

                    }
                    else
                    {
                        msg = "no rows found";
                         return false; //
                    }


                }
            }
            catch (Exception ex)
            {
                
                msg = ex.Message.ToString();
                return false;
            }
            finally
            {
                if (conn != null && conn.State != ConnectionState.Closed)
                    conn.Close();
            }
            
            return true;


        }

        public static bool GetNextBOMUploadBatchIDForImport(string uploadDir, string fileName, out int batchID) 
        {

            string sp = "getNextBOMUploadBatchIDForImport";
            CmdReturnStatus cmdStatus;
            List<SqlParameter> parameters = new List<SqlParameter>();
            int cmdTimeOut = ConfigHelper.ShortQueryTimeout;
            batchID = 0;
            SqlParameter param;

            param = new SqlParameter("@UploadDir", SqlDbType.VarChar, 200);
            param.Value = uploadDir;
            parameters.Add(param);

            param = new SqlParameter("@GPPBOMImportFileName", SqlDbType.VarChar, 100);
            param.Value = fileName;
            parameters.Add(param);

            param = new SqlParameter("@BOMUploadBatchID", SqlDbType.Int, 4);
            param.Direction = ParameterDirection.Output;
            parameters.Add(param);
            //

            cmdStatus = DBService.ExecuteNonQueryOutParam(sp, parameters, cmdTimeOut, "@BOMUploadBatchID",string.Empty);
            if (cmdStatus.error)
            {
                return false;
            }

            
            if (cmdStatus.outParam1.Value != null)
            {
                batchID = (int)cmdStatus.outParam1.Value;
            }
            
            if (batchID == 0)
                return false; 

            return true;

        }


        public static bool ProcessBOMUpload(int batchID, out int status, out string processMsg, out string errMsg)
        {
            status = -1;
            processMsg = string.Empty;
            errMsg = string.Empty;
            string sp = "processBOMFixedTest";
            CmdReturnStatus cmdStatus;
            List<SqlParameter> parameters = new List<SqlParameter>();
            int cmdTimeOut = ConfigHelper.LongQueryTimeout;
          
            SqlParameter param;

            param = new SqlParameter("@BOMUploadBatchID", SqlDbType.Int, 4);
            param.Value = batchID;
            parameters.Add(param);

            param = new SqlParameter("@Status", SqlDbType.Int, 4);
            param.Direction = ParameterDirection.Output;
            param.Value = status;
            parameters.Add(param);

            param = new SqlParameter("@ErrorMessage", SqlDbType.VarChar, 4000);
            param.Direction = ParameterDirection.Output;
            param.Value = processMsg;
            parameters.Add(param);
            //

            cmdStatus = DBService.ExecuteNonQueryOutParam(sp, parameters, cmdTimeOut, "@Status", "@ErrorMessage");
            if (cmdStatus.error)
            {
                errMsg = cmdStatus.errorMsg;
                return false;
            }


            if (cmdStatus.outParam1.Value != null)
            {
                status = (int)cmdStatus.outParam1.Value;
            }

            if (cmdStatus.outParam2.Value != null)
            {
                processMsg = (string)cmdStatus.outParam2.Value;
            }

            return true;

        }

        public static void CloseDB(SqlConnection scn)
        {
            scn.Close();
            scn.Dispose();
        }

        public static SqlConnection OpenDB()
        {

            SqlConnection scn = new SqlConnection(ConfigHelper.DBConnectionString);
            scn.Open();

            return scn;

        }

        /// <summary>
        /// Command execution retun values. May require further modifications
        /// </summary>
        public struct CmdReturnStatus
        {
            public int returnValue { get; set; }
            //public int outParamValue { get; set; }
            public SqlParameter outParam1 { get; set; }
            public SqlParameter outParam2 { get; set; }
            public string errorMsg { get; set; }
            public Boolean error { get; set; }
        }

    }

}

