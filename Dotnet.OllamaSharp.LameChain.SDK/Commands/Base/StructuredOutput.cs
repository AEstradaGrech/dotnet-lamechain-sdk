using Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutput.Attributes;
using System.Reflection;
using System.Text;

namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Base
{
    public class StructuredOutput
    {
        public string ToSystemMessage()
        {
            var sb = new StringBuilder();

            var header = jsonClassDescription().Trim();

            if (!string.IsNullOrEmpty(header))
                sb.AppendLine(header.Trim());

            GetType().GetProperties().ToList().ForEach(property => sb.AppendLine().AppendLine(instructionSectionFor(property.Name)));

            return sb.ToString();
        }

        protected virtual string jsonClassDescription()
        {
            var properties = Attribute.GetCustomAttributes(GetType());

            var sb = new StringBuilder();

            if(properties.Any(p => p.GetType() == typeof(OllamaJsonOutput)))
            {
                var classAttribute = (OllamaJsonOutput)properties.SingleOrDefault(p => p.GetType() == typeof(OllamaJsonOutput));

                if (!string.IsNullOrEmpty(classAttribute.Title))
                    sb.AppendLine($"# SCHEMA PURPOSE: {classAttribute.Title}");

                if(!string.IsNullOrEmpty(classAttribute.Description))
                {
                    var split = classAttribute.Description.Replace("\r", string.Empty).Split("\n");

                    var descSb = new StringBuilder();

                    foreach (string line in split)
                        descSb.AppendLine(line.Trim());

                    sb.Append($"> DESCRIPTION: {descSb.ToString()}");
                }
            }

            if (properties.Any(p => p.GetType() == typeof(OllamaJsonRequirement)))
            {
                sb.AppendLine("> OUTPUT REQUIREMENTS:");

                properties.Where(p => p.GetType() == typeof(OllamaJsonRequirement)).ToList().ForEach(req => sb.AppendLine(((OllamaJsonRequirement)req).Requirement));
            }
            return sb.ToString();
        }

        private string instructionSectionFor(string propertyName)
        {
            var properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            if (!properties.Any(p => p.Name == propertyName)) return string.Empty;

            var attributes = properties.SingleOrDefault(p => p.Name == propertyName).GetCustomAttributes<OllamaJsonProperty>();

            if (attributes.Count() == 0) return string.Empty;

            var sb = new StringBuilder()
                .AppendLine($"## PROPERTY NAME: {propertyName}");

            var auxSb = new StringBuilder();
            attributes.GroupBy(p => p.Title).ToList().ForEach(prop =>
            {
                auxSb.Clear();

                prop.ToList().ForEach(line => {

                    if (!string.IsNullOrEmpty(line.PromptDescription))
                        auxSb.AppendLine(line.PromptDescription);
                });

                sb.Append($"> {prop.Key.ToUpper()}: {auxSb.ToString()}");
            });

            var requirements = properties.SingleOrDefault(p => p.Name == propertyName).GetCustomAttributes<OllamaJsonRequirement>(); ;

            if(requirements.Count() > 0)
            {
                auxSb.Clear();

                requirements.ToList().ForEach(req => auxSb.AppendLine(req.Requirement));

                sb.AppendLine($"> REQUIREMENTS: {auxSb.ToString()}");
            }

            return sb.ToString().Trim();
        }
    }
}
