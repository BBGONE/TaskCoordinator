using Shared;
using Shared.Errors;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator.Interface;

namespace SSSB
{
    public class SSSBMessageProducer: IMessageProducer<SSSBMessage>
    {
        internal static readonly ILog _log = Log.GetInstance("SSSBMessageProducer");
        private TimeSpan DefaultWaitForTimeout = TimeSpan.FromSeconds(10);
        /// <summary>
        /// Число сообщений в пакете по умолчанию
        /// </summary>
        public const int DEFAULT_FETCH_SIZE = 1;
        private ISSSBService _sssbService;

        public SSSBMessageProducer(ISSSBService sssbService)
        {
            this._sssbService = sssbService;
            this.DefaultWaitForTimeout = sssbService.isQueueActivationEnabled ? TimeSpan.FromSeconds(10) : TimeSpan.FromSeconds(10);
        }

        public bool IsQueueActivationEnabled
        {
            get { return _sssbService.isQueueActivationEnabled; }
        }

        async Task<IEnumerable<SSSBMessage>> IMessageProducer<SSSBMessage>.ReadMessages(bool isPrimaryReader, int taskId, CancellationToken cancellation, object state)
        {
            SqlConnection dbconnection = (SqlConnection)state;
            //reading messages from the queue
            IDataReader reader = null;
            SSSBMessage[] messages = new SSSBMessage[0];
            try
            {
                if (isPrimaryReader)
                    reader = await SSSBManager.ReceiveMessagesAsync(dbconnection, this._sssbService.QueueName, 
                        DEFAULT_FETCH_SIZE, 
                        (int)DefaultWaitForTimeout.TotalMilliseconds, 
                        CommandBehavior.SingleResult | CommandBehavior.SequentialAccess, 
                        cancellation).ConfigureAwait(false);
                else
                    reader = await SSSBManager.ReceiveMessagesNoWaitAsync(dbconnection, this._sssbService.QueueName, 
                        DEFAULT_FETCH_SIZE, 
                        CommandBehavior.SingleResult | CommandBehavior.SequentialAccess,
                        cancellation).ConfigureAwait(false);

                //no result after cancellation
                if (reader == null)
                {
                    //return empty array
                    return messages;
                }

                var list = new LinkedList<SSSBMessage>();
                while (reader.Read())
                {
                    list.AddLast(this.FillMessageFromReader(reader));
                }

                messages = list.ToArray();
                return messages;
            }
            catch (Exception ex)
            {
                throw new PPSException(string.Format("ReadMessages error on queue: {0}, isPrimaryListener: {1}", this._sssbService.QueueName, isPrimaryReader), ex);
            }
            finally
            {
                if (reader != null)
                    reader.Close();
            }
        }

 
        /// <summary>
        /// Load message data from the data reader
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        private SSSBMessage FillMessageFromReader(IDataReader reader)
        {
            SSSBMessage message = new SSSBMessage();
            //conversation_group_id, 
            //conversation_handle,
            //message_sequence_number, 
            //service_name, 
            //service_contract_name, 
            //message_type_name, 
            //validation, 
            //message_body 
            message.ConversationGroupID = reader.GetGuid(0);
            message.ConversationHandle = reader.GetGuid(1);
            message.SequenceNumber = reader.GetInt64(2);
            message.ServiceName = reader.GetString(3);
            message.ContractName = reader.GetString(4);
            message.MessageType = reader.GetString(5);
            string validation = reader.GetString(6);
            if (validation == "X")
                message.ValidationType = MessageValidationType.XML;
            else if (validation == "E")
                message.ValidationType = MessageValidationType.Empty;
            else
                message.ValidationType = MessageValidationType.None;

            if (!reader.IsDBNull(7))
            {
                //Получаем размер сообщения
                message.Body = new byte[reader.GetBytes(7, 0, null, 0, 0)];
                reader.GetBytes(7, 0, message.Body, 0, message.Body.Length);
            }
            else
                message.Body = null;

            message.ServiceName = this._sssbService.Name;

            return message;
        }
    }
}
