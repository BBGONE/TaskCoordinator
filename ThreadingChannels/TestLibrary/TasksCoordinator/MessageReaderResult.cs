namespace TSM.TasksCoordinator
{
    public struct MessageReaderResult
    {
        public MessageReaderResult(bool isWorkDone, bool isRemoved)
        {
            this.IsWorkDone = isWorkDone;
            this.IsRemoved = isRemoved;
        }
        public readonly bool IsWorkDone;
        public readonly bool IsRemoved;
    }
}
