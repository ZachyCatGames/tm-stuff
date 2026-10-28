namespace Test;

public class TaskList
{
    public const int PriorityLevelCount = 10;

    List<ServiceTask>[] list;
    Lock listLock;

    public TaskList()
    {
        list = new List<ServiceTask>[PriorityLevelCount];
        for (int i = 0; i < PriorityLevelCount; i++)
        {
            list[i] = [];
        }

        listLock = new Lock();
    }

    public void Create() {}

    public void AddTask(ServiceTask task)
    {
        using var scope = listLock.EnterScope();
        uint priority = task.priority;
        list[priority].Add(task);
    }

    public void RemoveTask(ServiceTask task)
    {
        using var scope = listLock.EnterScope();
        foreach (var lvl in list)
        {
            if (lvl.Remove(task))
                return;

        }
    }

    public void ForEach(Action<ServiceTask> pred)
    {
        foreach (var lvl in list)
        {
            foreach (var task in lvl)
            {
                pred(task);
            }
        }
    }

    public void RemoveIf(Func<ServiceTask, bool> pred)
    {
        List<ServiceTask> toRemove = new();
        foreach (var lvl in list)
        {
            foreach (var task in lvl)
            {
                if (pred(task))
                {
                    toRemove.Add(task);
                }
            }
        }
        
        toRemove.ForEach((t) => this.RemoveTask(t));
    }

    public void RemoveAll()
    {
        foreach (var lvl in list)
        {
            lvl.Clear();
        }
    }

    public ServiceTask? FindByTaskById(uint taskId)
    {
        using var scope = listLock.EnterScope();
        foreach (var lvl in list)
        {
            foreach (var task in lvl)
            {
                
                if (task.taskId == taskId)
                {
                    return task;
                }
            }
        }

        return null;
    }

    public ServiceTask? CleanupAndFindTaskWithFollowup()
    {
        using var scope = listLock.EnterScope();
        ServiceTask? withFollowup = null;
        foreach (var lvl in list)
        {
            foreach (var task in lvl)
            {
                if (task.state != TaskState.InProgress)
                {
                    this.RemoveTask(task);
                }

                if (withFollowup != null && task.hasFollowup)
                {
                    withFollowup = task;
                }
            }
        }

        return withFollowup;
    }

    public bool IsTaskIdFree(uint taskId)
    {
        using var scope = listLock.EnterScope();
        foreach (var lvl in list)
        {
            foreach (var task in lvl)
            {
                if (task.taskId == taskId)
                    return false;
            }
        }

        return false;
    }
}
