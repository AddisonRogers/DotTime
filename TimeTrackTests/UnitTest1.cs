using TimeTrack;

namespace TimeTrackTests;

public class Tests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void Test1()
    {
        Assert.AreEqual(2, Program.wow(1, 1));
        Assert.That(Program.wow(1,1), Is.EqualTo(2));
    }
}