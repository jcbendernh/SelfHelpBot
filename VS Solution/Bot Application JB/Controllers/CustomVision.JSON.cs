using System.Runtime.Serialization;

namespace CustomVision.JSON
{
    [DataContract]
    public class Response
    {
        [DataMember(Name = "Predictions")]
        public Prediction[] Predictions { get; set; }

        [DataMember(Name = "Id")]
        public string Id { get; set; }

        [DataMember(Name = "Project")]
        public string Project { get; set; }

        [DataMember(Name = "Iteration")]
        public string Iteration { get; set; }

        [DataMember(Name = "Created")]
        public string Created { get; set; }

    }
    [DataContract]
    public class Prediction
    {
        [DataMember(Name = "TagId")]
        public string TagId { get; set; }

        [DataMember(Name = "Tag")]
        public string Tag { get; set; }

        [DataMember(Name = "Probability")]
        public double Probability { get; set; }

    }

}