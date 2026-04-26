public interface IGazeTarget
{
    void OnGazeEnter();
    void OnGazeStay(float progress);
    void OnGazeExit();
    void OnGazeComplete();
}