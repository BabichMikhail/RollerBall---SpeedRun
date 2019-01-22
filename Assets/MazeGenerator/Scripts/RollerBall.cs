using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class RollerBall : MonoBehaviour
{
    public GameObject ViewCamera;
    public bool UseManualBallController;

    private const int IntervalMilliseconds = 15;
    private const int DelayAfterBallHasNoEnergyMilliseconds = 10000;
    private const float Speed = 32f;

    private Rigidbody rigidBody;
    private int lastCallTime = -IntervalMilliseconds;
    private int iterations;
    private BallControl ballController;
    private int visitedCoinCount;
    private float successTime;

    private readonly List<Vector2> ballPositions = new List<Vector2>();
    private bool finished;
    private bool success;
    private bool outputWrote;

    private void Start()
    {
        rigidBody = GetComponent<Rigidbody>();
        Debug.Assert(rigidBody != null);
        lastCallTime = (int)(Time.time * 1000) + IntervalMilliseconds;
        ballController = UseManualBallController ? (BallControl) new ManualBallControl() : new AutoBallControl();
        try { ballController.SetMaze(); }
        catch (Exception exception) { if (Debug.isDebugBuild) throw exception; Application.Quit(1); }
        if (MazeDescription.IsConsoleRun())
            Time.timeScale = 100.0f;
    }

    private void Finish()
    {
        Debug.Log("Finish. " + visitedCoinCount);
        finished = true;
        SaveBallPosition();
        Time.timeScale = 1.0f;
        if (MazeDescription.IsConsoleRun()) {
            WriteOutputFile();
            Application.Quit();
        }
    }

    private void WriteOutputFile()
    {
        if (outputWrote)
            return;
        outputWrote = true;
        var lines = new List<string>{
            success ? "Success" : (MazeDescription.Coins - visitedCoinCount).ToString(),
            ballPositions.Count.ToString(),
        };
        foreach (var ballPosition in ballPositions)
            lines.Add(ballPosition.x + " " + ballPosition.y);
        File.WriteAllLines("output.txt", lines.ToArray());
    }

    private void SaveBallPosition()
    {
        ballPositions.Add(new Vector2(transform.position.x, transform.position.z));
    }

    private void FixedUpdate()
    {
        if (visitedCoinCount == MazeDescription.Coins) {
            if (!finished)
                successTime = Time.unscaledTime;
            Debug.Log("Success. Time:  " + successTime);
            success = true;
            Finish();
            return;
        }

        SaveBallPosition();
        var move = 0;
        try { move = ballController.GetMove(transform.position.x, transform.position.z); }
        catch (Exception exception) { if (Debug.isDebugBuild) throw exception; Application.Quit(1); }
        var torque = Vector3.zero;
        if ((move & BallControl.MoveTypeRight) != 0)
            torque += Vector3.right;
        if ((move & BallControl.MoveTypeBottom) != 0)
            torque += Vector3.back;
        if ((move & BallControl.MoveTypeLeft) != 0)
            torque -= Vector3.right;
        if ((move & BallControl.MoveTypeTop) != 0)
            torque -= Vector3.back;

        if (torque != Vector3.zero) {
            var newTorque = torque.normalized * Speed;
            rigidBody.AddTorque(new Vector3(newTorque.z, 0, -newTorque.x));
        }
        ++iterations;
        Debug.Log(iterations + " " + visitedCoinCount);

        if (ViewCamera != null) {
            var direction = (Vector3.up * 5 + Vector3.back) * 4;
            RaycastHit hit;
            ViewCamera.transform.position =
                Physics.Linecast(transform.position, transform.position + direction, out hit)
                    ? hit.point
                    : transform.position + direction;
            ViewCamera.transform.LookAt(transform.position);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag.Equals("Coin")) {
            Destroy(other.gameObject);
            ++visitedCoinCount;
        }
    }
}
