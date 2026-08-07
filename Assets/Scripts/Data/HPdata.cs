public struct HPData
{
    public int id;          // 谁的血量变化
    public float current;
    public float max;


    public HPData(int id,float current,float max)
    {
        this.id = id;
        this.current = current;
        this.max = max;
    }
}