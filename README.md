# TaskCoordinator
<b>Implements a subscriber-producer pattern for handling messages</b>

It has a message producer component (implements IMessageProducer interface) and the task coordinator creates tasks for handling messages. 
The messages are dispatched and handled by a class which implements IMessageDispatcher interface.<br/>
The main difference from other implementations of this pattern in that after reading a message from the queue the processing of the message goes
on the same thread.<br/>
This can be achieved by creating a number of long running threads and each thread reads and processes the messages. But this is
ineffective.<br/>
So in this implementation one of the tasks (threads) gets a role of a primary task and is different from the other tasks.
This role moves dynamically from one tasks to the next when the messages are read and processed.<br/>
Only one task at a time (the primary task) can wait for the messages in the queue, and the other tasks only process them.<br/><br/>

This is implemented like this:<br/>
at first the TaskCoordinator creates one task (thread), it is waiting for messages in the queue. 
Once the message is read, the TaskCoordinator creates another task which is waiting for messages, and the first task continues
processing of the message which have been read in it. And if the second task also reads a message, 
the TaskCoordinator creates another task which starts to wait for messages and the second task continues processing of the message.<br/><br/>
The TaskCoordinator has a parameter maxReadersCount, which caps the maximum number of created tasks. So after reaching this number of active tasks,
the TaskCoordinator does not create new ones even if the queue have unread messages.<br/>
<br/>
<b>With this implementation one of the tasks acts as an activator (waits for messages)</b><br/>
<b>This implementation is very good when you need to read and process messages in the same transaction.</b> 
This is the case with SQL Server Sevice Broker.
<br/><br/>
In case if you have an external activator, the TaskCoordinator has a parameter isQueueActivationEnabled. When isQueueActivationEnabled = true then
the TaskCoordinator does not have one always ON tasks. The first task is activated by an external activator. 
Once one task is activated all that pattern of tasks handling by the TaskCoordinator is repeated as before.
The only difference that the TaskCoordinator does not sustain one task to wait for the messages.
When there are no messages in the queue then all tasks are distroyed (after processing current messages) and then the TaskCoordinator needs a new external activation when in the queue will appear messages (it just need a kick to start).
<br/><br/>
LICENSE: Use it as you like!
