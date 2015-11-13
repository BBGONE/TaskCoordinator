namespace TasksCoordinator
{
	/// <summary>
	/// ���������
	/// </summary>
	public class Message
	{
		private byte[] _body;
		private string _messageType;
		private long _sequenceNumber;
		private string _serviceName;

	    /// <summary>
		/// ������ ���������.
		/// </summary>
		public byte[] Body
		{
			get { return _body; }
			set { _body = value; }
		}

		/// <summary>
		/// ��� ���������.
		/// </summary>
		public string MessageType
		{
			get { return _messageType; }
			set { _messageType = value; }
		}
		
		/// <summary>
		/// ���������� ����� ��������� � �������.
		/// </summary>
		public long SequenceNumber
		{
			get { return _sequenceNumber; }
			set { _sequenceNumber = value; }
		}

		/// <summary>
		/// �������� �������, �������� ���� ���������� ���������.
		/// </summary>
		public string ServiceName
		{
			get { return _serviceName; }
			set { _serviceName = value; }
		}
	}
}
