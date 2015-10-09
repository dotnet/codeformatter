// Import the utility functionality.

import jobs.generation.Utilities;

def project = 'dotnet/codeformatter'
// Define build string
def buildString = '''call "C:\\Program Files (x86)\\Microsoft Visual Studio 14.0\\Common7\\Tools\\VsDevCmd.bat" && build.cmd'''

// Generate the builds.

[true, false].each { isPR ->
    def newJob = job(Utilities.getFullJobName(project, '', isPR)) {
        label('windows')
        steps {
            batchFile(buildString)
        }
    }
    
    Utilities.simpleInnerLoopJobSetup(newJob, project, isPR, 'Windows Debug')
}