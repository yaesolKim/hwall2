/*using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

public class Ros_Ctrl : MonoBehaviour
{
    //control avatar
    private float hand_x, hand_y, hand_z, forward, turn;
    public float scale = 1.8f;
    private Animator avatar;
    public float movementSpeed = 1.5f;
    public float IKWeight = 0.9f; //0~1
    public Transform rightHandObj = null; //target: righthand
    public bool ikActive = false;

    //calculate orientation
    public GameObject fakeCollider;
    public float ori_y, ori_f, ori_rel, ori_distort;//, pos_rel_wall;
    private bool set_ori = false;
    private float[] ori = { 135.0f, 150.0f, 165.0f, 180.0f, 195.0f, 210.0f, 225.0f };

    //for demo2
    Vector3 targetPosition = new Vector3(2.8f, 0f, -5.63f);
    Vector3 targetPosition2 = new Vector3(0f, 0f, 0f);


    bool Connect = false;
    int btn = 0;

    bool modechange = false;
    bool movemode = true;

    Vector3 fix_pos = new Vector3(0, 0, 0);
    Vector3 fix_rot = new Vector3(0, 0, 0);

    Thread readThread;
    Socket client;

    //Socket server;
    int Port = 9190;
    string IP = "192.168.0.33"; //dell(glab WIFI)

    float[] avatar_vel = new float[5];
    byte[] SendM = Encoding.Default.GetBytes("Hello! I'm Unity.");
    byte[] ReceiveM = new byte[20];

    // Use this for initialization
    void Start()
    {
        avatar = GetComponent<Animator>();
        //ori_4
        //rightHandObj.rotation = Quaternion.Euler(-90, -20, 180);
        rightHandObj.rotation = Quaternion.Euler(-90, 0, 180);
        //ori_7
        //rightHandObj.rotation = Quaternion.Euler(-90, 40, 180);

        Debug.Log("Start!!!!!!!!!!!!!!!!!!!");

        
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (btn == 0 && Connect == true)
        {
            readThread = new Thread(new ThreadStart(ReceiveData));
            readThread.IsBackground = true;
            readThread.Start();
            btn = 1;
            Debug.Log("ROS is connected!");
            
        }

        else if (btn == 1 && Connect == true)
        {
            SetAvatar();
            avatar.transform.position = Vector3.Lerp(avatar.transform.position, targetPosition, movementSpeed * Time.deltaTime);
        }

        else if (btn == 1 && Connect == false)
        {
            stopThread();
            btn = 0;
            Debug.Log("ROS is disconnected!");
        }

        forward -= 0.1f;
    }


    public void PressButton(int btn)
    {
        switch (btn)
        {
            case 1:
                Connect = true;
                Debug.Log("CONNECT BUTTON");
                break;
            case 2:
                Connect = false;
                Debug.Log("DISCONNECT BUTTON");
                break;
        }
    }

    void SetAvatar()
    {
        ////////////start of move mode///////////////
        if ((avatar_vel[1] == 0) && (avatar_vel[2] == 0) && (avatar_vel[0] == 0))
        {
            ////////////change mode: touch -> move
            if (!movemode)
            {
                Debug.Log("mode change: touch -> move");

                //move backward
                avatar.transform.position -= (avatar.transform.forward*3)/2;
                fakeCollider.SetActive(true);

                ikActive = false;
                movemode = true;
            }

            ikActive = false;

            if (avatar_vel[3] > 0)
            {
                forward = 1;
            }

            turn = avatar_vel[4];

            if (forward > 0)
            {
                //move forward
                avatar.transform.position += avatar.transform.forward * Time.deltaTime * movementSpeed;
            }

            if (turn == 1)
            {
                //turn right
                avatar.transform.Rotate(0, 20 * Time.deltaTime, 0);
            }
            else if (turn == -1)
            {
                //turn left
                avatar.transform.Rotate(0, -20 * Time.deltaTime, 0);
            }

            movemode = true;

        }
        ////////////end of move mode///////////////


        ////////////start of touch mode
        else // distance between human head and robot is close enough.
        {
            if (movemode) //mode change: move -> touch
            {
                Debug.Log("mode change: move -> touch");
                //modechange = true;

                //use ori_f from onColliderEnter function
                set_ori = true;
                fakeCollider.SetActive(false);

                //rotate
                float rot = ori_distort;
                avatar.transform.localEulerAngles = new Vector3(0, rot, 0);
                //for demo2
                //nothing.

                //for general purpose
                //avatar.transform.position += avatar.transform.forward*2/3;
                ikActive = true;

                fix_pos = avatar.transform.position;
                fix_rot = avatar.transform.eulerAngles;
            } //end of mode change

            ikActive = true;

            //avatar.transform.position = fix_pos;
            avatar.transform.eulerAngles = fix_rot;

            hand_x = avatar_vel[1] * scale;
            hand_y = avatar_vel[2] * scale;
            hand_z = -avatar_vel[0] * scale - 0.2f;
            if (hand_z > 0.37)
                hand_z = 0.37f;
            targetPosition2 = new Vector3(hand_x, hand_y, hand_z);
            rightHandObj.transform.localPosition = Vector3.Lerp(rightHandObj.transform.localPosition, targetPosition2, 1);
            //rightHandObj.transform.localPosition = Vector3.MoveTowards(rightHandObj.transform.localPosition, targetPosition2, 1);

            movemode = false;
        }
        ////////////end of touch mode

        //set float forward
        avatar.SetFloat("Forward", forward);

    }


    void OnCollisionEnter(Collision col)
    {
        if (col.collider.tag == "FakeWall")
        {
            for (int i = 0; i < col.contacts.Length; i++)
            {
                //calculate relative orientation between avatar and collide fake wall (avatar ori - fakewall ori)
                ori_rel = gameObject.transform.eulerAngles.y - col.collider.transform.eulerAngles.y;
            }
            
            //Debug.Log("1. ori_avatar : " + gameObject.transform.eulerAngles.y);
            //Debug.Log("2. ori_fakewall : " + col.collider.transform.eulerAngles.y);

            if (ori_rel < 0)
                ori_y = ori_rel + 360;
            else
                ori_y = ori_rel;

            float min = 15;

            //find nearest orientation
            for (int i = 0; i < ori.Length; i++)
            {
                if (Math.Abs(ori[i] - ori_y) < min)
                {
                    min = Math.Abs(ori[i] - ori_y);
                    ori_f = ori[i];
                    set_ori = true;
                }
            }

            Debug.Log("ori_f : " + ori_f);

            //new orientation of avatar.
            if (ori_rel < 0)
                ori_distort = col.collider.transform.eulerAngles.y + ori_f - 360;
            else
                ori_distort = col.collider.transform.eulerAngles.y + ori_f;
            
            Debug.Log("4. new ori_avatar : " + ori_distort);
            
            //stop move forward
            forward = 0f;
        }
    }





    void OnApplicationQuit()
    {
        stopThread();
    }

    private void stopThread()
    {
        if (readThread.IsAlive)
        {
            readThread.Abort();
        }
        client.Close();
    }


    void OnAnimatorIK()
    {
        if (avatar)
        {
            if (ikActive)
            {
                // Set the right hand target position and rotation, if one has been assigned
                if (rightHandObj != null)
                {
                    avatar.SetIKPositionWeight(AvatarIKGoal.RightHand, IKWeight);
                    avatar.SetIKRotationWeight(AvatarIKGoal.RightHand, IKWeight);
                    avatar.SetIKPosition(AvatarIKGoal.RightHand, rightHandObj.position);
                    avatar.SetIKRotation(AvatarIKGoal.RightHand, rightHandObj.rotation);
                }
            }

            else
            {
                avatar.SetIKPositionWeight(AvatarIKGoal.RightHand, 0);
                avatar.SetIKRotationWeight(AvatarIKGoal.RightHand, 0);
            }
        }

    }

    private void ReceiveData()
    {
        client = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        IPEndPoint ipep = new IPEndPoint(IPAddress.Parse(IP), Port);
        client.SendTo(SendM, SendM.Length, SocketFlags.None, ipep); //synchronous

        IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
        EndPoint remote = (EndPoint)(sender);

        while (true)
        {
            try
            {
                //recieve avatar_vel from ROS
                ReceiveM = new byte[20];
                client.ReceiveFrom(ReceiveM, ref remote);

                for (int i = 0; i < avatar_vel.Length; i++)
                {
                    avatar_vel[i] = System.BitConverter.ToSingle(ReceiveM, i * 4);
                }
            }
            catch (Exception err)
            {
                print(err.ToString());
            }
        }
    }


}
*/

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

public class Ros_Ctrl : MonoBehaviour
{
    //control avatar
    private float hand_x, hand_y, hand_z, forward, turn;
    public float scale = 1.8f;
    private Animator avatar;
    public float movementSpeed = 1.5f;
    public float IKWeight = 0.9f; //0~1
    public Transform rightHandObj = null; //target: righthand
    public bool ikActive = false;

    //calculate orientation
    public GameObject fakeCollider;
    public float ori_y, ori_f, ori_rel, ori_distort, pos_rel_wall, control_mode;
    private byte[] b_ori_f, b_pos_rel_wall, b_control_mode;
    private bool set_ori = false;
    private bool set_control_mode = false;
    private float[] ori = { 135.0f, 150.0f, 165.0f, 180.0f, 195.0f, 210.0f, 225.0f };

    //ik hand posision
    Vector3 targetPosition = new Vector3(0f, 0f, 0f);

    bool Connect = false;
    int btn = 0;

    int initial_send = 0;

    bool modechange = false;
    bool movemode = true;

    Vector3 fix_pos = new Vector3(0, 0, 0);
    Vector3 fix_rot = new Vector3(0, 0, 0);

    Thread readThread;
    Socket client;

    //Socket server;
    int Port = 9188;
    string IP = "192.168.0.33"; //dell(glab WIFI)

    float[] avatar_vel = new float[5];
    byte[] SendM = Encoding.Default.GetBytes("Hello! I'm Unity.");
    byte[] ReceiveM = new byte[20];

    // Use this for initialization
    void Start()
    {
        avatar = GetComponent<Animator>();
        //ori_4
        rightHandObj.rotation = Quaternion.Euler(-90, -20, 180);

        //ori_7
        //rightHandObj.rotation = Quaternion.Euler(-90, 40, 180);

        Debug.Log("Start!!!!!!!!!!!!!!!!!!!");
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (btn == 0 && Connect == true)
        {
            readThread = new Thread(new ThreadStart(ReceiveData));
            readThread.IsBackground = true;
            readThread.Start();
            btn = 1;
            Debug.Log("ROS is connected!");
        }

        else if (btn == 1 && Connect == true)
        {
            SetAvatar();
        }

        else if (btn == 1 && Connect == false)
        {
            stopThread();
            btn = 0;
            Debug.Log("ROS is disconnected!");
        }

        forward -= 0.1f;
    }


    public void PressButton(int btn)
    {
        switch (btn)
        {
            case 1:
                Connect = true;
                Debug.Log("CONNECT BUTTON");
                break;
            case 2:
                Connect = false;
                Debug.Log("DISCONNECT BUTTON");
                break;
        }
    }

    void SetAvatar()
    {
        ////////////start of move mode///////////////
        if ((avatar_vel[1] == 0) && (avatar_vel[2] == 0) && (avatar_vel[0] == 0))
        {
            ////////////change mode: touch -> move
            if (!movemode)
            {
                Debug.Log("mode change: touch -> move\n");

                //move backward
                avatar.transform.position -= (avatar.transform.forward * 3) / 2;
                fakeCollider.SetActive(true);

                //ikActive = false;
                //movemode = true;

                set_ori = false;
                set_control_mode = false;
            }

            ikActive = false;
            movemode = true;

        }
        ////////////end of move mode///////////////


        ////////////start of touch mode
        else // distance between human head and robot is close enough.
        {
            if (movemode) //mode change: move -> touch
            {
                Debug.Log("mode change: move -> touch\n");
                //modechange = true;

                //use ori_f from onColliderEnter function
                set_ori = true;
                fakeCollider.SetActive(false);

                //move front and rotate
                float rot = ori_distort;
                avatar.transform.localEulerAngles = new Vector3(0, rot, 0);
                avatar.transform.position += avatar.transform.forward * 2 / 3;
                ikActive = true;

                fix_pos = avatar.transform.position;
                fix_rot = avatar.transform.eulerAngles;
            } //end of mode change

            ikActive = true;

            //avatar.transform.position = fix_pos;
            //avatar.transform.eulerAngles = fix_rot;

            hand_x = avatar_vel[1] * scale;
            hand_y = avatar_vel[2] * scale;
            hand_z = -avatar_vel[0] * scale - 0.2f;

            if (hand_z > 0.37)
                hand_z = 0.37f;
            targetPosition = new Vector3(hand_x, hand_y, hand_z);
            rightHandObj.transform.localPosition = Vector3.Lerp(rightHandObj.transform.localPosition, targetPosition, 1);


            movemode = false;
        }
        ////////////end of touch mode

        //set float forward
        avatar.SetFloat("Forward", forward);

    }


    void OnCollisionEnter(Collision col)
    {
        if (col.collider.tag == "FakeWall")
        {
            for (int i = 0; i < col.contacts.Length; i++)
            {
                //calculate relative orientation between avatar and collide fake wall (avatar ori - fakewall ori)
                ori_rel = gameObject.transform.eulerAngles.y - col.collider.transform.eulerAngles.y;
            }

            if (ori_rel < 0)
                ori_y = ori_rel + 360;
            else
                ori_y = ori_rel;

            float min = 15;

            //find nearest orientation
            for (int i = 0; i < ori.Length; i++)
            {
                if (Math.Abs(ori[i] - ori_y) < min)
                {
                    min = Math.Abs(ori[i] - ori_y);
                    ori_f = ori[i];
                    set_ori = true;
                }
            }

            if (col.collider.gameObject.name == "fdoor1")
            {
                //impedance control.
                control_mode = 1;
            }

            else // fwalls...
            {
                //cartesian control.
                control_mode = 2;
            }

            set_control_mode = true;

            Debug.Log("collision -- ori_f : " + ori_f + ", control mode: " + control_mode);

            //new orientation of avatar.
            if (ori_rel < 0)
                ori_distort = col.collider.transform.eulerAngles.y + ori_f - 360;
            else
                ori_distort = col.collider.transform.eulerAngles.y + ori_f;

            //Debug.Log("4. new ori_avatar : " + ori_distort);

            //stop move forward
            forward = 0f; // should be changed
        }
    }



    void OnApplicationQuit()
    {
        stopThread();
    }

    private void stopThread()
    {
        if (readThread.IsAlive)
        {
            readThread.Abort();
        }
        client.Close();
    }


    void OnAnimatorIK()
    {
        if (avatar)
        {
            if (ikActive)
            {
                // Set the right hand target position and rotation, if one has been assigned
                if (rightHandObj != null)
                {
                    avatar.SetIKPositionWeight(AvatarIKGoal.RightHand, IKWeight);
                    avatar.SetIKRotationWeight(AvatarIKGoal.RightHand, IKWeight);
                    avatar.SetIKPosition(AvatarIKGoal.RightHand, rightHandObj.position);
                    avatar.SetIKRotation(AvatarIKGoal.RightHand, rightHandObj.rotation);
                }
            }

            else
            {
                avatar.SetIKPositionWeight(AvatarIKGoal.RightHand, 0);
                avatar.SetIKRotationWeight(AvatarIKGoal.RightHand, 0);
            }
        }

    }

    private void ReceiveData()
    {
        client = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        IPEndPoint ipep = new IPEndPoint(IPAddress.Parse(IP), Port);
        if (initial_send == 0)
        {
            client.SendTo(SendM, SendM.Length, SocketFlags.None, ipep); //synchronous
            initial_send++;
        }

        IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
        EndPoint remote = (EndPoint)(sender);

        while (true)
        {
            try
            {
                //recieve avatar_vel from ROS
                ReceiveM = new byte[20];
                client.ReceiveFrom(ReceiveM, ref remote);

                for (int i = 0; i < avatar_vel.Length; i++)
                {
                    avatar_vel[i] = System.BitConverter.ToSingle(ReceiveM, i * 4);
                }

                //send message (to ROS) : orif, relative pos, control mode
                SendM = new byte[12];

                if (set_ori && set_control_mode)
                {
                    pos_rel_wall = 1f;

                    //modechange = false;

                    b_ori_f = System.BitConverter.GetBytes(ori_f);
                    b_pos_rel_wall = System.BitConverter.GetBytes(pos_rel_wall);
                    b_control_mode = System.BitConverter.GetBytes(control_mode);

                    Debug.Log("sending control mode: " + control_mode);

                    Buffer.BlockCopy(b_ori_f, 0, SendM, 0, 4);
                    Buffer.BlockCopy(b_pos_rel_wall, 0, SendM, 4, 4);
                    Buffer.BlockCopy(b_control_mode, 0, SendM, 8, 4);

                    client.SendTo(SendM, SendM.Length, SocketFlags.None, ipep);//send ori_f & pos_rel_wall & control_mode
                }

                set_control_mode = false;
                control_mode = 0f;
                pos_rel_wall = 0f;
            }
            catch (Exception err)
            {
                print(err.ToString());
            }
        }
    }


}
