export function Home() {
  return (
    <div>
      <h1>AI PMO Insight</h1>
      <p>
        Turns fragmented project data into portfolio-level management insight, decision support,
        and early-warning signals. This is the proof-of-concept walking skeleton.
      </p>
      <ul>
        <li><a href="https://learn.microsoft.com/aspnet/core">ASP.NET Core</a> minimal APIs with a lightweight in-process mediator (Clean Architecture)</li>
        <li><a href="https://react.dev/">React</a> + <a href="https://vite.dev/">Vite</a> for the client, <a href="https://picocss.com/">Pico CSS</a> for styling</li>
        <li>EF Core + PostgreSQL, persisting <strong>findings and citations</strong> rather than raw client data</li>
      </ul>
      <p>
        Open the <strong>Project findings</strong> page to upload a (dummy Orbit-shaped) file, run a
        stub analysis, and read the resulting findings for a project — each cited back to its source.
      </p>
    </div>
  );
}
