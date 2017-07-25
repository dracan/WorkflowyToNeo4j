<Query Kind="Program">
  <NuGetReference>Neo4jClient</NuGetReference>
  <Namespace>Neo4jClient</Namespace>
  <Namespace>Newtonsoft.Json</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
</Query>

async Task Main()
{
    var nodes = ProcessWorkflowy(Path.Combine(Path.GetDirectoryName(Util.CurrentQueryPath), @"Workflowy.txt"));
    
    nodes.Count().Dump("Total Nodes");

    var maxNestLevel = nodes.Max(x => x.Level);
    var arr = new Node[maxNestLevel + 2];

    var dc = new DumpContainer();
    dc.Dump();

    var pb = new Util.ProgressBar();
    pb.Dump();

    using (var client = new GraphClient(new Uri("http://localhost:7474/db/data"), "neo4j", "blah"))
	{
        await client.ConnectAsync();

        await client.Cypher
            .Match("(n)")
            .OptionalMatch("(n)-[r]-()")
            .Delete("n, r")
            .ExecuteWithoutResultsAsync();

        var rootNode = new Node { Id = Guid.NewGuid(), Name = "Workflowy" };
        await client.Cypher
            .Create("(node:Node {node})")
            .WithParam("node", rootNode)
            .ExecuteWithoutResultsAsync();
            
        arr[0] = rootNode;
        
        var n = 0;

        foreach (var node in nodes)
        {
            if (node.Level < 0)
            {
                throw new Exception($"Invalid level: {node.Level}");
            }
 
            arr[node.Level + 1] = node;
            
            var parentNode = arr[node.Level];

            await client.Cypher
                .Match("(parent:Node)")
                .Where((Node parent) => parent.Id == parentNode.Id)
                .Create("(parent)-[:CHILD]->(child:Node {node})")
                .WithParam("node", node)
                .ExecuteWithoutResultsAsync();
                
            n++;
            pb.Percent = (int)(((decimal)n / (decimal)nodes.Count) * 100m);
            dc.Content = $"Processed {n} out of {nodes.Count} nodes";
        }
	}
}

List<Node> ProcessWorkflowy(string filename)
{
    var lines = File.ReadAllLines(filename);
    
    var nodes = new List<Node>();

    foreach (var line in lines)
    {
        var match = Regex.Match(line, @"^(\s*)- (.*?)$");

        if (match.Success)
        {
            nodes.Add(new Node
            {
                Id = Guid.NewGuid(),
                Name = match.Groups[2].Value,
                Level = match.Groups[1].Value.Length / 2,
            });
        }
    }
    
    return nodes;
}

public class Node
{
	public Guid Id { get; set; }
	public string Name { get; set; }
    public int Level { get; set;}
}