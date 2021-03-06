# TaskCoordinator
<b>Implements a subscriber-producer pattern for handling messages</b>
<br/><br/>
It has a message producer component (implements IMessageProducer interface) and the task coordinator creates tasks for handling messages. 
The messages are dispatched and handled by a class which implements IMessageDispatcher interface.<br/>
The main difference from other implementations of this pattern in that after reading a message from the queue the processing of the message goes
on the same thread.<br/>
This can be achieved by creating a number of long running threads (or tasks) and each thread reads and processes the messages. But this is
ineffective.<br/><br/>
So in this implementation one of the tasks (threads) gets a role of the primary task and is different from the other tasks.
This role moves dynamically from one tasks to the next when the messages are read and processed.<br/>
Only one task at a time (the primary task) can wait for the messages in the queue, and the other tasks only process them.
<br/><br/>
This is implemented like this:<br/>
at first the TaskCoordinator creates one task (thread), it is waiting for messages in the queue. 
Once the message is read, the TaskCoordinator creates another task which is waiting for messages, and the first task continues
processing of the message which have been read in it. And if the second task also reads a message, 
the TaskCoordinator creates another task which starts to wait for messages and the second task continues processing of the message.<br/><br/>
The TaskCoordinator has a parameter <i>maxReadersCount</i>, which caps the maximum number of created tasks. So after reaching this number of active tasks,
the TaskCoordinator does not create new ones even if the queue has unread messages.<br/>
<br/>
<b>With this implementation one of the tasks acts as an activator (waits for messages)</b><br/>
<b>This implementation is very usefull when you need to read and process messages in the same transaction.</b> 
(<i>This is the case with SQL Server Sevice Broker</i>)
<br/><br/>
The TaskCoordinator has one more parameter <i>maxReadParallelism </i>, which is 4 by default. It can boost performance if tweaked in some cases when
reading messages from the queue is a lengthy operation - (<i>mostly in cases when the time taken to obtain a message from the queue is on a par with the time taken
 to process the message. But it rarely happens in  practice - because message processing is usually longer than reading it from the queue</i>)
<br/>
The repository contains a console application which uses the TaskCoordinator. 
It can be used as a lab and a testing ground for usage of the TaskCoordinator.
<br/>
<i><b>
For example, the testing application implemented a TransformBlock (and some other TPL blocks that act like ones from the official TPL Dataflow library) which was made for testing the TaskCoordinator in a simplified setup. But it (TaskCoordinator) really shines for the transactional message processing. Sql Server Service Broker and
optionally Oracle Advanced Queuing uses transactional message processing. So I used the TaskCoordinator for my TaskBroker implementation which uses Sql Server Service Broker to provide the messages. And it saves resources by using only one connection to message queue while idle (when currently no messages in the queue).
</b></i>
<br/><br/>
LICENSE: MIT LICENSE
