using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;

namespace ClusterTester
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the interface name "IClusterTesterService" in both code and config file together.
    [ServiceContract]
    public interface IClusterTesterService
    {
        [OperationContract]
        [FaultContract(typeof(String))]
        int[] RunTask(int taskId, int taskTime, double failureChance);
    }
}
