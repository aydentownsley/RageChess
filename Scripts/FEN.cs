using System;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using TMPro;
using BNG;
using UnityEngine.UI;


// #if !PLATFORM_STANDALONE_WIN


public class FEN : MonoBehaviour
{
    public GameObject torch;
    public TextMeshProUGUI Dialogue;
    public AudioSource Beh;
    public Text screen;
    public Color player_color;
    public List<SnapZone> spaces = new List<SnapZone>();
    private string turn = " w", FEN_string = "", move_string = "", path;
    private int spaceIdx, rowIdx, spaceValue = 1;

    void Start()
    {
        getFEN();
    }

    /// <summary>
    /// Gets path to engine executable
    /// Gets FEN string of current board state
    /// Creates a new thread for stockfish to run on
    /// Calls for thread start
    /// </summary>
    public void stockfish_process()
    {
        this.path = Application.dataPath;
        screen.text += "Path: " + path + "\n";
        getFEN();

        Thread stockfish_thread = new Thread(run_stockfish);
        StartCoroutine(wait_for_stockfish(stockfish_thread));
    }

    /// <summary>
    /// Starts new thread and stockfish process
    /// waits until thread has completed running
    /// Joins thread to get move from engine
    /// </summary>
    IEnumerator wait_for_stockfish(Thread stockfish_thread)
    {
        torch.GetComponent<ParticleSystem>().startColor = Color.green;
        torch.GetComponent<ParticleSystem>().gravityModifier = 0.09f;
        Dialogue.text = "Thinking...";
        // This code is just incase stockfish library moves and needs to
        // be found on the headset
        // string filepath = "/data/data/com.RageInc.RageChess/lib";
        // if (!Directory.Exists(filepath))
        // {
        //     screen.text += "dir not found\n";
        // } else {
        //     screen.text += "dir found\n";
        //     if (File.Exists(filepath + "/libstockfish.so"))
        //     {
        //         screen.text += "libstockfish.so found\n";
        //     } else {
        //         screen.text += "libstockfish.so not found\n";
        //     }
        //     string[] dir = Directory.GetFiles(filepath);
        //     foreach (string file in dir)
        //         screen.text += '\n' + file;
        // }
        stockfish_thread.Start();
        screen.text += "thread started\n";
        while (stockfish_thread.IsAlive)
        {
            screen.text += "thread alive\n";
            yield return null;
        }
        stockfish_thread.Join();
        screen.text += "thread joined\n";
        screen.text += "move data: " + move_string + "\n";
        move();
        torch.GetComponent<ParticleSystem>().startColor = Color.white;
        torch.GetComponent<ParticleSystem>().gravityModifier = 0f;
        // stockfish finished and has moved when this is printed
        Dialogue.text = this.move_string.Split(' ')[1].Substring(0,2) + " to " + this.move_string.Split(' ')[1].Substring(2,2);

        // FOR white suggestion repeat process but somehow highlight suggested move
        // stockfish_process();
        // dont call move, make a suggestion function
    }

    /// <summary>
    /// Runs stockfish process on its seperate thread
    ///
    /// Note: Do not use Unity API in this function
    /// Unity is not thread safe.
    /// </summary>
    public void run_stockfish()
    {
        UnityEngine.Debug.Log("stockfish process started");
        this.move_string = "";
#if UNITY_EDITOR
        UnityEngine.Debug.Log("UNITY_EDITOR");
        Process stockfish = new Process();
        ProcessStartInfo startInfo = new ProcessStartInfo()
        {
            FileName = this.path + "/Stockfish/stockfish.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            RedirectStandardInput = true
        };
        // This code is just incase stockfish library moves and needs to
        // be found on the headset
        // string filepath = "/data/data/com.whozi.stockfish/lib";
        // if (!Directory.Exists(filepath))
        // {
        //     screen.text = "dir not found";
        // }else{
        //     screen.text = "dir found";
        //     string[] dir = Directory.GetFiles(filepath);
        //     foreach (string file in dir)
        //         screen.text += '\n' + file;
        // }
#elif UNITY_ANDROID
        UnityEngine.Debug.Log("UNITY_ANDROID");
        string filepath = "/data/data/com.RageInc.RageChess/lib";
        Process stockfish = new Process();
        ProcessStartInfo startInfo = new ProcessStartInfo()
        {
            FileName = filepath + "/libstockfish.so",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            RedirectStandardInput = true
        };
#endif
        stockfish.StartInfo = startInfo;
        stockfish.Start();
        StreamWriter sw = stockfish.StandardInput;

        sw.WriteLine("uci");
        sw.WriteLine("ucinewgame");
        sw.WriteLine("position fen " + this.FEN_string + " b");
        sw.WriteLine("go movetime 3000");
        while (stockfish.StandardOutput.EndOfStream == false)
        {
            this.move_string = stockfish.StandardOutput.ReadLine();
            if (this.move_string.Contains("bestmove"))
            {
                UnityEngine.Debug.Log("Stockfish output: " + this.move_string);
                break;
            }
        }
        stockfish.Close();
    }

    /// <summary>
    /// Takes output from stockfish, parses it, and convertes
    /// value to actions for gameobjects.
    /// </summary>
    public void move()
    {
        GameObject piece = null;
        GameObject startSpace = null;
        GameObject endSpace = null;
        string[] parse;
        string move = "";

        // Dialogue.text += "Move has been called\n";
        parse = this.move_string.Split(' ');
        // Dialogue.text += "\n" + this.move_string;
        // screen.text += "\n" + this.move_string;

        foreach (string s in parse)
            UnityEngine.Debug.Log(s);
        UnityEngine.Debug.Log(parse.Length);
        move = parse[1];
        UnityEngine.Debug.Log("move: " + move);
        if (move == "(none)")
            UnityEngine.Debug.Log("Checkmate");
        else
        {
            piece = GameObject.Find(move.Substring(0,2)).transform.GetChild(1).gameObject;
            startSpace = GameObject.Find(move.Substring(0, 2));
            endSpace = GameObject.Find(move.Substring(2, 2));
            UnityEngine.Debug.Log("piece: " + piece.name);
            startSpace.GetComponent<SnapZone>().ReleaseAll();
            if (endSpace.transform.childCount == 2)
            {
                Destroy(endSpace.transform.GetChild(1).gameObject);
                endSpace.GetComponent<SnapZone>().ReleaseAll();
                Beh.Play();
            }
            piece.transform.position = endSpace.transform.position;
            endSpace.GetComponent<SnapZone>().GrabGrabbable(piece.GetComponent<Grabbable>());
            UnityEngine.Debug.Log(piece.transform.parent);
        }
    }

    /// <summary>
    /// loops through the spaces
    /// on the board and creates
    /// a FEN string for Stockfish
    /// </summary>
    public void getFEN()
    {
        this.FEN_string = "";
        string temp = "";
        int count = 1;
        int empty = 0;
        foreach (SnapZone space in spaces)
        {
            if (space.HeldItem != null)
            {
                if (empty == 0)
                    temp += space.HeldItem.name.ToString();
                else
                {
                    temp += empty.ToString() + space.HeldItem.name.ToString();
                    empty = 0;
                }
            }
            else
                empty += 1;

            if (count % 8 == 0 && count != 0 && count != 64)
            {
                if (empty == 0)
                    this.FEN_string += temp + "/";
                else
                {
                    this.FEN_string += temp + empty.ToString() + "/";
                    empty = 0;
                }
                temp = "";
            }
            else if (count == 64)
            {
                if (empty == 0)
                    this.FEN_string += temp;
                else
                {
                    this.FEN_string += temp + empty.ToString();
                    empty = 0;
                }
                temp = "";
            }

            count += 1;
        }
        UnityEngine.Debug.Log(this.FEN_string);
        // screen.text += "\n" + this.FEN_string;
    }
}

// #endif
