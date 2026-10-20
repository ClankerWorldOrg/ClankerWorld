// Main can arrive before a queue workflow finishes its nonblocking bookkeeping.
// Reuse its required jobs, from one exact-SHA run attempt, rather than waiting for
// the whole workflow's conclusion. Missing evidence leaves main's full run enabled.
module.exports = async function findQueueRun({ github, context, core }) {
  let runId = '';
  try {
    const runs = await github.paginate(github.rest.actions.listWorkflowRuns, {
      ...context.repo, workflow_id: 'ci.yml', event: 'merge_group',
      head_sha: context.sha, per_page: 100,
    });
    // Never fall back to an older green run after a later run failed or restarted.
    const run = runs.filter(run => run.event === 'merge_group' && run.head_sha === context.sha)
      .sort((a, b) => b.id - a.id)[0];
    if (run && Number.isInteger(run.run_attempt) && run.run_attempt > 0) {
      const jobs = await github.paginate(github.rest.actions.listJobsForWorkflowRunAttempt, {
        ...context.repo, run_id: run.id, attempt_number: run.run_attempt, per_page: 100,
      });
      const required = ['verify', 'windows-documentation', 'windows-provider-storage'];
      if (required.every(name => {
        const matches = jobs.filter(job => job.name === name);
        return matches.length === 1 && matches[0].head_sha === context.sha &&
          matches[0].status === 'completed' && matches[0].conclusion === 'success';
      })) runId = String(run.id);
    }
  } catch (error) {
    core.warning(`Could not confirm the merge queue's required jobs: ${error.message}`);
  }
  core.setOutput('run', runId);
  core.info(runId
    ? `The merge queue's required jobs passed in run ${runId} on this exact commit, so nothing else runs.`
    : "The merge queue didn't pass this exact commit, so everything runs.");
};
