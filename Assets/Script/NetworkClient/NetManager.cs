using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Sockets;
using System;
using System.Linq;
using System.Diagnostics.Tracing;


public class NetManager
{
    //定义socket
    static Socket socket;
    //接收缓冲区
    static ByteArray readBuff;
    //写入队列
    static Queue<ByteArray> writeQueue;

    //是否正在连接
    static bool isConnecting = false;

    //是否正在关闭
    static bool isClosing = false;

    //消息列表
    static List<MsgBase> msgList = new List<MsgBase>();
    //消息列表长度
    static int msgCount = 0;
    //每一次Update处理的消息数量
    readonly static int MAX_MESSAGE_FIRE = 10;


    //是否启用心跳
    public static bool isUsePing = true;
    //心跳间隔时间
    public static int pingInterval = 30;
    //上一次发送PING的时间
    static float lastPingTime = 0;
    //上次收到PONG的时间
    static float lastPongTime = 0;



    //----------------------------------监听结构------------------------------------------

    //------------------------------1. 网络事件监听----------------------------------------

    //事件委托类型
    public delegate void EventListener(String err);
    //事件监听列表
    private static Dictionary<NetEvent, EventListener> eventListeners = new Dictionary<NetEvent, EventListener>();


    //添加事件监听
    public static void AddEventListener(NetEvent netEvent, EventListener listener)
    {
        //如果字典中已存在事件，则添加事件
        if (eventListeners.ContainsKey(netEvent))
        {
            eventListeners[netEvent] += listener;
        }
        //如果不存在，则新增事件
        else
        {
            eventListeners[netEvent] = listener;
        }
    }

    //删除事件监听
    public void RemoveEventListener(NetEvent netEvent, EventListener listener)
    {
        if (eventListeners.ContainsKey(netEvent))
        {
            eventListeners[netEvent] -= listener;

            //删除
            if (eventListeners[netEvent] == null)
            {
                eventListeners.Remove(netEvent);

            }
        }
    }

    //分发事件
    private static void FireEvent(NetEvent netEvent, String err)
    {
        if (eventListeners.ContainsKey(netEvent))
        {
            eventListeners[netEvent](err);
        }
    }

    //-------------------------------2. 网络消息监听---------------------------------------------

    //消息委托类型
    public delegate void MsgListener(MsgBase msgBase);

    //消息监听列表
    private static Dictionary<string, MsgListener> msgListeners = new Dictionary<string, MsgListener>();

    //添加消息监听
    public static void AddMsgListener(string msgName, MsgListener listener)
    {
        //添加
        if (msgListeners.ContainsKey(msgName))
        {
            msgListeners[msgName] += listener;
        }
        //新增
        else
        {
            msgListeners[msgName] = listener;
        }
    }

    //删除消息监听
    public static void RemoveMsgListener(string msgName, MsgListener listener)
    {
        if (msgListeners.ContainsKey(msgName))
        {
            msgListeners[msgName] -= listener;

            //删除
            if (msgListeners[msgName] == null)
            {
                msgListeners.Remove(msgName);
            }
        }


    }



    //-------------------------------------------------------------------------------------------

    //----------------------------分发消息-------------------------------
    private static void FireMsg(string msgName, MsgBase msgBase)
    {
        if (msgListeners.ContainsKey(msgName))
        {
            msgListeners[msgName](msgBase);
        }
    }


    //-------------------------------初始化状态-----------------------------------
    private static void InitState()
    {
        //Socket
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        //接收缓冲区
        readBuff = new ByteArray();

        //写入队列
        writeQueue = new Queue<ByteArray>();

        //是否正在链接
        isConnecting = false;

        //是否正在关闭
        isClosing = false;

        //消息列表
        msgList = new List<MsgBase>();
        //消息列表长度
        msgCount = 0;


        //上一次发送PING的时间
        lastPingTime = Time.time;
        //上一次收到PONG的时间
        lastPongTime = Time.time;
        //建通PONG协议（不重复添加）
        if (!msgListeners.ContainsKey("MsgPong"))
        {
            AddMsgListener("MsgPong", OnMsgPong);
        }


    }

    //处理PONG监听的消息
    private static void OnMsgPong(MsgBase msgBase)
    {
        lastPongTime = Time.time;
    }

    //--------------------------------------连接-----------------------------------
    public static void Connect(string ip, int port)
    {
        //状态判断
        //1.已经链接时按下连接键
        if (socket != null && socket.Connected)
        {
            Debug.Log("Connect fail, already connected!");
            return;
        }
        //2. 正在链接时按下链接键
        if (isConnecting)
        {
            Debug.Log("Connect fail, isConnecting");
            return;
        }

        //3. 未连接是按下连接键
        //初始化成员
        InitState();
        //参数设置
        socket.NoDelay = true;
        //连接
        isConnecting = true;
        socket.BeginConnect(ip, port, ConnectCallback, socket);
    }

    //Connect回调
    private static void ConnectCallback(IAsyncResult ar)
    {
        try
        {
            Socket socket = (Socket)ar.AsyncState;
            socket.EndConnect(ar);
            Debug.Log("Socket Connect Succ");
            FireEvent(NetEvent.ConnectSucc, "");
            isConnecting = false;

            //开始接收
            socket.BeginReceive(readBuff.bytes, readBuff.writeIdx, readBuff.remain, 0, ReceiveCallback, socket);
        }
        catch (SocketException ex)
        {
            Debug.Log("Socket Connect fail" + ex.ToString());
            FireEvent(NetEvent.ConnectFail, ex.ToString());
            isConnecting = false;
        }
    }

    //Receive回调
    public static void ReceiveCallback(IAsyncResult ar)
    {
        try
        {
            Socket socket = (Socket)ar.AsyncState;

            //获取接收数据长度
            int count = socket.EndReceive(ar);
            if (count == 0)
            {
                Close();
                return;
            }
            readBuff.writeIdx += count;

            //处理消息：解析协议，并把协议对象放置到消息列表中
            OnReceiveData();

            //继续接收数据
            if (readBuff.remain < 8)
            {
                readBuff.MoveBytes();
                readBuff.ReSize(readBuff.length * 2);
            }
            socket.BeginReceive(readBuff.bytes, readBuff.writeIdx, readBuff.remain, 0, ReceiveCallback, socket);

        }
        catch (SocketException ex)
        {
            Debug.Log("Socket Receive Fail" + ex.ToString());

        }
    }

    //数据处理
    public static void OnReceiveData()
    {
        //消息长度
        if (readBuff.length <= 2)
        {
            return;
        }

        //获取消息体长度
        int readIdx = readBuff.readIdx;
        byte[] bytes = readBuff.bytes;
        Int16 bodyLength = (Int16)((bytes[readIdx + 1] << 8) | bytes[readIdx]);
        if (readBuff.length < bodyLength)
        {
            return;
        }
        readBuff.readIdx += 2;

        //解析协议名
        int nameCount = 0;
        string protoName = MsgBase.DecodeName(readBuff.bytes, readBuff.readIdx, out nameCount);

        if (protoName == "")
        {
            Debug.Log("OnReceiveData MsgBase.DecodeName fail");
            return;
        }
        readBuff.readIdx += nameCount;

        //解析协议体
        int bodyCount = bodyLength - nameCount;
        MsgBase msgBase = MsgBase.Decode(protoName, readBuff.bytes, readBuff.readIdx, bodyCount);
        readBuff.readIdx += bodyCount;
        readBuff.CheckAndMoveBytes();

        //添加到消息队列
        lock (msgList)
        {
            msgList.Add(msgBase);
        }

        msgCount++;

        //继续读取消息
        if (readBuff.length > 2)
        {
            OnReceiveData();
        }

    }

    //---------------------------关闭连接-----------------------------
    public static void Close()
    {
        //状态判断1:只有存在socket 和 建立连接后才能关闭
        if (socket == null || !socket.Connected)
        {
            return;

        }

        //状态判断2：正在连接时候不能关闭
        if (isConnecting)
        {
            return;
        }

        //状态判断3：还有数据在发送时候,等待到发送完再关闭
        if (writeQueue.Count > 0)
        {
            isClosing = true;
        }
        else//没有数据发送 
        {
            socket.Close();
            FireEvent(NetEvent.Close, "");
        }
    }

    //------------------------------发送数据--------------------------------
    public static void Send(MsgBase msg)
    {
        //状态判断
        if (socket == null || !socket.Connected)
        {
            return;
        }

        if (isConnecting)
        {
            return;
        }

        if (isClosing)
        {
            return;
        }

        //数据编码
        byte[] nameBytes = MsgBase.EncodeName(msg);
        byte[] bodyBytes = MsgBase.Encode(msg);

        int len = nameBytes.Length + bodyBytes.Length;
        byte[] sendBytes = new byte[2 + len];

        //组装长度
        sendBytes[0] = (byte)(len % 256);
        sendBytes[1] = (byte)(len / 256);

        //组装名字
        Array.Copy(nameBytes, 0, sendBytes, 2, nameBytes.Length);

        //组装消息体
        Array.Copy(bodyBytes, 0, sendBytes, 2 + nameBytes.Length, bodyBytes.Length);

        //写入队列
        ByteArray ba = new ByteArray(sendBytes);
        int count = 0;  //writeQueue的长度
        lock (writeQueue)
        {
            writeQueue.Enqueue(ba);
            count = writeQueue.Count;
        }

        //send
        if (count == 1)
        {
            socket.BeginSend(sendBytes, 0, sendBytes.Length, 0, SendCallback, socket);
        }

    }

    //Send回调
    public static void SendCallback(IAsyncResult ar)
    {
        //获取state, EndSend 的处理
        Socket socket = (Socket)ar.AsyncState;

        //状态判断
        if (socket == null || !socket.Connected)
        {
            return;
        }

        //结束发送
        int count = socket.EndSend(ar);

        //获取写入队列第一条数据
        ByteArray ba;
        lock (writeQueue)
        {
            ba = writeQueue.First();
        }

        //This increments the readIdx by the number of bytes that were successfully sent (count).
        ba.readIdx += count;

        //If ba.length == 0, it means the entire message has been sent
        if (ba.length == 0)
        {
            lock (writeQueue)
            {
                // so the message can be removed from the writeQueue.
                writeQueue.Dequeue();
                //The next message in the queue (if any) is retrieved for sending.
                ba = writeQueue.First();

            }
        }

        //继续发送
        //If there's still data to send (either from the current message or the next message in the queue),
        //call BeginSend again to continue sending the remaining data.
        if (ba != null)
        {
            socket.BeginSend(ba.bytes, ba.readIdx, ba.length, 0, SendCallback, socket);

        }
        //正在关闭
        else if (isClosing)
        {
            socket.Close();
        }

    }

    //-------------------Update-------------------------------

    public static void Update()
    {
        MsgUpdate();
        PingUpdate();
    }

    //更新消息
    public static void MsgUpdate()
    {
        //初步判断，提升效率
        if (msgCount == 0)
        {
            return;
        }

        //重复处理消息
        for (int i = 0; i < MAX_MESSAGE_FIRE; i++)
        {
            //获取第一条消息
            MsgBase msgBase = null;
            lock (msgList)
            {
                if (msgList.Count > 0)
                {
                    msgBase = msgList[0];
                    msgList.RemoveAt(0);
                    msgCount--;
                }
            }

            //分发消息
            if (msgBase != null)
            {
                FireMsg(msgBase.protoName, msgBase);
            }
            //没有消息了
            else
            {
                break;
            }

        }
    }

    //更新发送ping协议
    private static void PingUpdate()
    {
        //判断是否启用
        if (!isUsePing)
        {
            return;
        }

        //判断当前时间与上一次发送MsgPing协议的时间间隔，控制发送ping
        if (Time.time - lastPingTime > pingInterval)
        {
            MsgPing msgPing = new MsgPing();
            Send(msgPing);
            lastPingTime = Time.time;
        }

        //判断当前时间与上一次接收MsgPong协议的时间间隔，控制关闭连接
        if (Time.time - lastPongTime > pingInterval * 4)
        {
            Close();
        }
    }




    //---------------------------ENUM--------------------------
    //事件
    public enum NetEvent
    {
        ConnectSucc = 1,
        ConnectFail = 2,
        Close = 3,

    }
    //---------------------------------------------------------



}
