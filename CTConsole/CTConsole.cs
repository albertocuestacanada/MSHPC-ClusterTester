using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;

using Microsoft.Hpc.Scheduler;
using Microsoft.Hpc.Scheduler.Properties;
using Microsoft.Hpc.Scheduler.Session;

using System.ComponentModel;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;

namespace CTConsole
{
    class CTConsole
    {
        static void Main(string[] args)
        {
            var runParameters = new Dictionary<string,string>();
            foreach (var row in File.ReadAllLines(args[0]))
            {
                if (row.StartsWith("#")) continue;
                runParameters.Add(row.Split('=')[0], row.Split('=')[1]);
            }

            // ToDo: Test the run parameters for sanity

            ClusterRun cr = new ClusterRun();

            cr.doSubmit(runParameters);

            Console.Out.Write("Please press Enter to exit.");
            Console.In.ReadLine();
        }
    }

    class ClusterRun
    {
        private int m_start;  // timing:
        private int m_stop;
        private Semaphore m_signal;

        public void doSubmit(Dictionary<string,string> runParameters)
        {
            m_start = System.Environment.TickCount;
            
            //
            // Configure session with cluster:
            //
            string[] parameterKeys = {"HeadNode","Service","JobTemplate","Tasks","TaskTime"};
            foreach (string parameterKey in parameterKeys)
                if (!runParameters.ContainsKey(parameterKey)) outputError(parameterKey);
            
            string headNode = runParameters["HeadNode"];
            SessionStartInfo info = new SessionStartInfo(headNode, runParameters["Service"]);

            info.SessionResourceUnitType = SessionUnitType.Core;
            if (runParameters.ContainsKey("MinCores")) info.MinimumUnits = Convert.ToInt32(runParameters["MinCores"]);
            if (runParameters.ContainsKey("MaxCores")) info.MaximumUnits = Convert.ToInt32(runParameters["MaxCores"]);
            if (runParameters.ContainsKey("Priority")) info.SessionPriority = Convert.ToInt32(runParameters["Priority"]);
            
            info.Secure = false;  // generally off in the classroom:
            info.BrokerSettings.SessionIdleTimeout = 15000;  // 15 secs:
            info.JobTemplate = runParameters["JobTemplate"];


            int tasks = Convert.ToInt32(runParameters["Tasks"]);
            int taskTime = Convert.ToInt32(runParameters["TaskTime"]);

            double taskFailureChance = 0.0;
            if (runParameters.ContainsKey("TaskFailureChance")) taskFailureChance = Convert.ToDouble(runParameters["TaskFailureChance"]);
            int taskTimeDeviation = 0;
            if (runParameters.ContainsKey("TaskTimeDeviation")) taskTimeDeviation = Convert.ToInt32(runParameters["TaskTimeDeviation"]);
            int taskThrottling = 0;
            if (runParameters.ContainsKey("TaskThrottling")) taskThrottling = Convert.ToInt32(runParameters["TaskThrottling"]);

            //
            // User is prompted for run-as credentials, then session
            // is opened for business:
            //
            Session.SetInterfaceMode(false /*GUI*/, (IntPtr)0);
            Console.WriteLine("Creating session: {0}ms", System.Environment.TickCount - m_start);
            using (var session = Session.CreateSession(info))
            {
                Console.WriteLine("Session Created: {0}ms", System.Environment.TickCount - m_start);
                
                var mode = (info.Secure) ? SecurityMode.Transport : SecurityMode.None;
                var binding = new NetTcpBinding(mode);
                using (var proxy = new BrokerClient<ClusterTesterService.IClusterTesterService>(session, binding))
                {
                    m_signal = new Semaphore(0, 1);
                    proxy.SetResponseHandler<ClusterTesterService.RunTaskResponse>(doSubmit_ServiceCallback, m_signal);

                    Console.WriteLine("Connected to session {0}: {1}ms", session.Id, System.Environment.TickCount - m_start);

                    //
                    // Make the N service calls:
                    //
                    Console.WriteLine("Sending requests: {0}", System.Environment.TickCount - m_start);
                    Random random = new Random();
                    int thisTaskTime;
                    for (int i = 0; i < tasks; i++)
                    {
                        if (taskTimeDeviation != 0) thisTaskTime = Math.Max(0,gaussian(random, taskTime, taskTimeDeviation));
                        else thisTaskTime = taskTime;

                        if (taskThrottling > 0) Thread.Sleep(taskThrottling);

                        proxy.SendRequest(new ClusterTesterService.RunTaskRequest(i, thisTaskTime, taskFailureChance));
                    }
                    proxy.EndRequests();
                    Console.WriteLine("End requests: {0}ms", System.Environment.TickCount - m_start);

                    // 
                    // wait on callbacks
                    //
                    m_signal.WaitOne();
                    session.Close();
                }
            }

            Console.WriteLine("Exit program: {0}ms", System.Environment.TickCount - m_start);
            m_stop = System.Environment.TickCount;
        }

        private void doSubmit_ServiceCallback(BrokerResponse<ClusterTesterService.RunTaskResponse> response, object state)
        {
            string result;

            try
            {
                int[] r = response.Result.RunTaskResult;
                result = string.Format("Task: {0} - Runtime on node: {1}ms", r[0], r[2]); // Hardcoded fields to an array defined on the service - bad boy
            }
            catch (FaultException fex)
            {
                result = "Task failed: " + fex.ToString();
            }
            Console.WriteLine(result);

            if (response.IsLastResponse)
                ((Semaphore)state).Release();
        }

        private int gaussian(Random random, int mean, int stdDev) {
            double u1 = random.NextDouble(); //these are uniform(0,1) random doubles
            double u2 = random.NextDouble();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) *
                         Math.Sin(2.0 * Math.PI * u2); //random normal(0,1)
            return Convert.ToInt32(Convert.ToDouble(mean) + Convert.ToDouble(stdDev) * randStdNormal); //random normal(mean,stdDev^2)
        }

        private void outputError(string errorKey)
        {
            var errors = new Dictionary<string,string>();
            errors.Add("HeadNode", "HeadNode parameter missing (string)");
            errors.Add("Service", "Service parameter missing (string)");
            errors.Add("MinCores","MinCores parameter missing (int)");
            errors.Add("MaxCores","MaxCores parameter missing (int)");
            errors.Add("JobTemplate","JobTemplate parameter missing (string)");
            errors.Add("Tasks", "Tasks parameter missing (int)");
            errors.Add("TaskTime", "TaskTime parameter missing (int)");

            Console.Error.WriteLine(errors[errorKey]);
        }
    }
}
