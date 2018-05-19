using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using System.Threading;

namespace ClusterTester
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the class name "ClusterTesterService" in code, svc and config file together.
    public class ClusterTesterService : IClusterTesterService
    {
        public int[] RunTask(int taskId, int taskTime, double failureChance)
        {
            int m_start = System.Environment.TickCount;

            Thread.Sleep(taskTime);

            int[] response = 
            {
                taskId,
                taskTime,
                System.Environment.TickCount - m_start
            };

            Random r = new Random();
            if (r.NextDouble() < failureChance)
            {
                throw new FaultException<String>("ClusterTester generated Exception",
                    new FaultReason("ClusterTester generated Exception"),
                    new FaultCode("Sender"));
            }

            return response;
        }
    }
}
