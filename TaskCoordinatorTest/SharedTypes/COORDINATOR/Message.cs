namespace TasksCoordinator
{
	/// <summary>
	/// Сообщение
	/// </summary>
	public class Message
	{
		private byte[] _body;
		private string _messageType;
		private long _sequenceNumber;
		private string _serviceName;

	    /// <summary>
		/// Данные сообщения.
		/// </summary>
		public byte[] Body
		{
			get { return _body; }
			set { _body = value; }
		}

		/// <summary>
		/// Тип сообщения.
		/// </summary>
		public string MessageType
		{
			get { return _messageType; }
			set { _messageType = value; }
		}
		
		/// <summary>
		/// Порядковый номер сообщения в очереди.
		/// </summary>
		public long SequenceNumber
		{
			get { return _sequenceNumber; }
			set { _sequenceNumber = value; }
		}

		/// <summary>
		/// Название сервиса, которому было направлено сообщение.
		/// </summary>
		public string ServiceName
		{
			get { return _serviceName; }
			set { _serviceName = value; }
		}
	}
}
