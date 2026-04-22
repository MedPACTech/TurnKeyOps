using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MedInsights.Lib.Dtos
{
    public class PromptTemplateDto
    {
        public string Id { get; set; } //partitionkey  example: PromptTemplate
        public string PromptTemplateId { get; set; } //rowkey example: patient-1 

        public string Entity { get; set; } // example: Patient
        public string PromptTemplateName { get; set; } // example: Create Patient
        public string Action { get; set; } // example: create, update, delete
        public string Prompt { get; set; } // example: Create a new patient record in the system
    }
}