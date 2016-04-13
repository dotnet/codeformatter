// Import the utility functionality.
import jobs.generation.Utilities;
// Import functionality for job reporting
import jobs.generation.JobReport;

def project = GithubProject
def branch = GithubBranchName
// Define build string
def buildString = '''call "C:\\Program Files (x86)\\Microsoft Visual Studio 14.0\\Common7\\Tools\\VsDevCmd.bat" && build.cmd'''

// Generate the builds.

[true, false].each { isPR ->
    def newJob = job(Utilities.getFullJobName(project, '', isPR)) {
        steps {
            batchFile(buildString)
        }
    }
    
    Utilities.setMachineAffinity(newJob, 'Windows_NT', 'latest-or-auto')
    Utilities.standardJobSetup(newJob, project, isPR, "*/${branch}")
    if (isPR) {
        Utilities.addGithubPRTriggerForBranch(newJob, branch, 'Windows Debug')
    }
    else {
        Utilities.addGithubPushTrigger(newJob)
    }
}

// Generate the job report
JobReport.Report.generateJobReport(out)