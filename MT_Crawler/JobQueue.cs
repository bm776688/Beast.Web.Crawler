using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Microsoft.Advertising.Analytics.SharedService
{
    public class AsyncJobQueue<T> where T : class
    {
		PriorityQueue<T> jobQueue = new PriorityQueue<T>();
        int maxQueuedJob = 5000;
        int maxConcurrentJob = 50;
        int pendingJob = 0;
        int asyncTimeout = 60000; // 60s

        public delegate WaitHandle AsyncJobProcesser(T job);
        public delegate void JobHandler(T job);

        AsyncJobProcesser jobProcesser;
        JobHandler abortHandler;

        public AsyncJobQueue(AsyncJobProcesser jobProcesser, JobHandler abortHandler)
        {
            this.jobProcesser = jobProcesser;
            this.abortHandler = abortHandler;
        }

        public int MaxQueuedJob
        {
            get { return maxQueuedJob; }
            set { maxQueuedJob = value; }
        }
        public int MaxConcurrentJob
        {
            get { return maxConcurrentJob; }
            set { maxConcurrentJob = value; }
        }
        public int AsyncTimeout
        {
            get { return asyncTimeout; }
            set { asyncTimeout = value; }
        }

        #region TPS control stuff.
        bool enableTpsControl = false;
        int tps = 10;

        System.Timers.Timer tpsTimer;
        int numInTimeSlice = 1;
        const int minTimeSlice = 100;
        void StartTpsTimer()
        {
            this.StopTpsTimer();

            int intervalSlice = (int)Math.Ceiling( 1000.0 / tps);
            if (intervalSlice < minTimeSlice)
            {
                numInTimeSlice = (int)(minTimeSlice / intervalSlice);
                intervalSlice = minTimeSlice;
            }
            tpsTimer = new System.Timers.Timer(intervalSlice);
            tpsTimer.Elapsed += new System.Timers.ElapsedEventHandler(tpsTimer_Elapsed);
            tpsTimer.Start();

            this.enableTpsControl = true;
        }

        void tpsTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            ProcessJobTimmerCallback();
        }

        void StopTpsTimer()
        {
            this.enableTpsControl = false;

            if (tpsTimer != null)
            {
                tpsTimer.Stop();
                tpsTimer.Dispose();
                tpsTimer = null;
            }
        }

        public int Tps
        {
            get { return tps; }
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException("Tps", value, "Tps must be positive.");
                }
                tps = value;
            }
        }

        public bool EnableTpsControl
        {
            set
            {
                if (!this.stopProcess)
                {
                    throw new InvalidOperationException();
                }
                this.enableTpsControl = value;
            }
            get { return enableTpsControl; }
        }

        #endregion

        #region start/stop process
        bool stopProcess = true;
        Thread worker;
        public void StartProcess()
        {
            lock (this)
            {
                if (stopProcess)
                {
                    stopProcess = false;
                    if (enableTpsControl)
                    {
                        StartTpsTimer();
                    }
                    else
                    {
                        worker = new Thread(ProcessJobLoop);
                        worker.IsBackground = true;
                        worker.Start();
                    }
                }
            }
        }
        public void StopProcess()
        {
            lock (this)
            {
                if (!stopProcess)
                {
                    stopProcess = true;
                    if (enableTpsControl)
                    {
                        StopTpsTimer();
                    }
                    else
                    {
                        lock (jobQueue)
                        {
                            Monitor.PulseAll(jobQueue);
                        }
                        worker = null;
                    }
                }
            }
        }
        #endregion

        #region Job processing
        private int m_consumersWaiting = 0;
        private int m_producersWaiting = 0;
        public void EnqueueJob(T job)
        {
            lock (jobQueue)
            {
                while (jobQueue.Count >= MaxQueuedJob)
                {
                    m_producersWaiting++;
                    Monitor.Wait(jobQueue);
                    m_producersWaiting--;
                }
                jobQueue.Enqueue(job);
                if (m_consumersWaiting > 0)
                {
                    Monitor.PulseAll(jobQueue);
                }
            }
        }
        void ProcessJobLoop(object obj)
        {
            while (!stopProcess)
            {
                T job = null;
                lock (jobQueue)
                {
                    while (!stopProcess && (jobQueue.Count == 0 || pendingJob >= MaxConcurrentJob))
                    {
                        m_consumersWaiting++;
                        Monitor.Wait(jobQueue);
                        m_consumersWaiting--;
                    }
                    if (!stopProcess)
                    {
                        job = jobQueue.Dequeue();
                        if (m_producersWaiting > 0)
                        {
                            Monitor.PulseAll(jobQueue);
                        }
                    }
                }
                if (job != null)
                {
                    ProcessJob(job);
                }
            }
        }
        void ProcessJobTimmerCallback()
        {
            int num = numInTimeSlice;
            while (num-- > 0)
            {
                T job = null;
                if (jobQueue.Count > 0 && pendingJob < MaxConcurrentJob)
                {
                    lock (jobQueue)
                    {
                        if (jobQueue.Count > 0 && pendingJob < MaxConcurrentJob)
                        {
                            job = jobQueue.Dequeue();
                        }
                    }
                }
                if (job != null)
                {
                    ProcessJob(job);
                }
                else
                {
                    break;
                }
            }
        }
        void ProcessJob(T job)
        {
            WaitHandle completeHandle = jobProcesser(job);
            if (completeHandle != null)
            {
                ThreadPool.RegisterWaitForSingleObject(completeHandle, JobDoneCallback, job, AsyncTimeout, true);
                Interlocked.Increment(ref pendingJob);
            }
        }

        void JobDoneCallback(object state, bool timedOut)
        {
            if (timedOut)
            {
                abortHandler(state as T);
            }

            Interlocked.Decrement(ref pendingJob);
            lock (jobQueue)
            {
                if (m_consumersWaiting > 0 || m_producersWaiting > 0)
                {
                    Monitor.PulseAll(jobQueue);
                }
            }
        }
        #endregion
    }
}
