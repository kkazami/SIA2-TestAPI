param([int]$Port = 5168)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
dotnet build "$root/TestAPI/TestAPI.csproj" --no-restore -c Release -p:UseAppHost=false -o "$root/TestResults/build"
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$database = "$root/TestResults/check-$([guid]::NewGuid().ToString('N')).db"
$start = [System.Diagnostics.ProcessStartInfo]::new('dotnet')
$start.CreateNoWindow = $true
$start.UseShellExecute = $false
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
foreach ($argument in @("$root/TestResults/build/TestAPI.dll", '--urls', "http://127.0.0.1:$Port", '--environment', 'Development', '--contentRoot', "$root/TestAPI", "--ConnectionStrings:Booking=Data Source=$database")) { $start.ArgumentList.Add($argument) }
$process = [System.Diagnostics.Process]::Start($start)
$stdout = $process.StandardOutput.ReadToEndAsync()
$stderr = $process.StandardError.ReadToEndAsync()
$base = "http://127.0.0.1:$Port/api/v1"
$checks = [System.Collections.Generic.List[string]]::new()
function Request($label, $method, $path, $token, $body, $expected) {
    $options = @{ Uri = "$base/$path"; Method = $method; SkipHttpErrorCheck = $true }
    if ($token) { $options.Headers = @{ Authorization = "Bearer $token" } }
    if ($null -ne $body) { $options.ContentType = 'application/json'; $options.Body = ($body | ConvertTo-Json -Depth 10 -Compress) }
    $response = Invoke-WebRequest @options
    if ([int]$response.StatusCode -ne $expected) { throw "$label expected $expected, got $($response.StatusCode): $($response.Content)" }
    $checks.Add("PASS $label ($expected)")
    if ($response.Content) { return ($response.Content | ConvertFrom-Json) }
}
function Login($email) { Request "Login $email" POST 'auth/login' '' @{ email = $email; password = 'DemoPass123!' } 200 }
try {
    $ready = $false
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        if ($process.HasExited) { throw "API exited: $($stderr.Result)" }
        try { $null = Invoke-WebRequest "http://127.0.0.1:$Port/swagger/v1/swagger.json"; $ready = $true; break } catch { Start-Sleep -Milliseconds 250 }
    }
    if (!$ready) { throw 'API did not start.' }
    $null = Request 'Anonymous protected GET' GET users '' $null 401
    $null = Request 'Invalid token' GET users 'invalid' $null 401
    $null = Request 'Bad password' POST auth/login '' @{email='admin@example.com';password='bad'} 401
    $null = Request 'Invalid login email' POST auth/login '' @{email='bad';password='bad'} 400
    $admin = Login 'admin@example.com'; $tutor = Login 'tutor@example.com'; $student = Login 'student@example.com'
    $users = Request 'List users' GET users $admin.token $null 200
    if (($users | ConvertTo-Json -Depth 10) -match 'passwordHash') { throw 'Password hash leaked.' }
    $null = Request 'Tutor denied users' GET users $tutor.token $null 403
    $new = Request 'Create account' POST users $admin.token @{name='Another Student';email='other@example.com';password='DemoPass123!';role='Student'} 201
    $other = Login 'other@example.com'
    $null = Request 'Anonymous account creation denied' POST users '' @{name='N';email='n@example.com';password='DemoPass123!';role='Student'} 401
    $null = Request 'Duplicate email' POST users $admin.token @{name='N';email='OTHER@example.com';password='DemoPass123!';role='Student'} 409
    $null = Request 'Invalid user input' POST users $admin.token @{name='';email='bad';password='short';role='Student'} 400
    $null = Request 'Invalid role' POST users $admin.token @{name='N';email='role@example.com';password='DemoPass123!';role='Owner'} 400
    $updated = Request 'Partial account update' PATCH "users/$($new.id)" $admin.token @{name='Changed'} 200
    if ($updated.Email -ne 'other@example.com' -or $updated.Role -ne 'Student') { throw 'Partial PATCH cleared other fields.' }
    $null = Request 'Empty user patch' PATCH "users/$($new.id)" $admin.token @{} 400
    $null = Request 'Missing user' PATCH 'users/999999' $admin.token @{name='Changed'} 404
    $null = Request 'Protect last admin' DELETE "users/$($admin.user.id)" $admin.token $null 409
    $date = [DateTime]::UtcNow.Date.AddDays(3)
    function SlotBody($hour, $tutorId) { @{tutorId=$tutorId;startTime=$date.AddHours($hour).ToString('o');endTime=$date.AddHours($hour+1).ToString('o')} }
    $null = Request 'Student cannot create slot' POST availability $student.token (SlotBody 0 $tutor.user.id) 403
    $null = Request 'Invalid tutor' POST availability $admin.token (SlotBody 0 $student.user.id) 400
    $null = Request 'Invalid dates' POST availability $tutor.token @{tutorId=$tutor.user.id;startTime=$date.AddHours(1).ToString('o');endTime=$date.ToString('o')} 400
    $slot1 = Request 'Create slot' POST availability $tutor.token (SlotBody 0 $tutor.user.id) 201
    $null = Request 'Reject tutor overlap' POST availability $tutor.token (SlotBody 0 $tutor.user.id) 409
    $null = Request 'Update slot' PUT "availability/$($slot1.id)" $tutor.token (SlotBody 0 $tutor.user.id) 200
    $slot2 = Request 'Create reschedule slot' POST availability $tutor.token (SlotBody 2 $tutor.user.id) 201
    $disposable = Request 'Create disposable slot' POST availability $tutor.token (SlotBody 4 $tutor.user.id) 201
    $null = Request 'Delete slot' DELETE "availability/$($disposable.id)" $tutor.token $null 200
    $null = Request 'Missing slot' DELETE 'availability/999999' $tutor.token $null 404
    $available = Request 'View availability' GET availability $student.token $null 200
    if (@($available).Count -ne 2) { throw 'Available-slot list incorrect.' }
    $null = Request 'Tutor cannot book' POST appointments $tutor.token @{availabilityId=$slot1.id} 403
    $null = Request 'Missing booking slot' POST appointments $student.token @{availabilityId=999999} 404
    $null = Request 'Invalid booking ID' POST appointments $student.token @{availabilityId=0} 400
    $appointment = Request 'Book appointment' POST appointments $student.token @{availabilityId=$slot1.id} 201
    $null = Request 'Double booking denied' POST appointments $other.token @{availabilityId=$slot1.id} 409
    $available = Request 'Booked slot hidden' GET availability $student.token $null 200
    if (@($available).Count -ne 1 -or $available[0].id -ne $slot2.id) { throw 'Booked slot remained available.' }
    $null = Request 'List own appointments' GET appointments $student.token $null 200
    $otherAppointments = Request 'Other student list filtered' GET appointments $other.token $null 200
    if (@($otherAppointments).Count -ne 0) { throw 'Other student could see bookings.' }
    $null = Request 'List tutor appointments' GET appointments $tutor.token $null 200
    $null = Request 'Admin monitors appointments' GET appointments $admin.token $null 200
    $detail = Request 'Appointment detail' GET "appointments/$($appointment.id)" $student.token $null 200
    if ($detail.StudentName -ne 'Demo Student' -or !$detail.StartTime) { throw 'Appointment detail missing names/times.' }
    $null = Request 'Other student access denied' GET "appointments/$($appointment.id)" $other.token $null 403
    $null = Request 'Missing appointment' GET 'appointments/999999' $student.token $null 404
    $null = Request 'Empty appointment patch' PATCH "appointments/$($appointment.id)" $student.token @{} 400
    $null = Request 'Invalid appointment status' PATCH "appointments/$($appointment.id)" $student.token @{status='Invalid'} 400
    $null = Request 'Student cannot confirm' PATCH "appointments/$($appointment.id)" $student.token @{status='Confirmed'} 403
    $null = Request 'Tutor confirms' PATCH "appointments/$($appointment.id)" $tutor.token @{status='Confirmed'} 200
    $null = Request 'Cannot edit booked slot' PUT "availability/$($slot1.id)" $tutor.token (SlotBody 0 $tutor.user.id) 409
    $null = Request 'Cannot delete booked slot' DELETE "availability/$($slot1.id)" $tutor.token $null 409
    $null = Request 'Reschedule appointment' PATCH "appointments/$($appointment.id)" $student.token @{availabilityId=$slot2.id} 200
    $null = Request 'Cancel appointment' PATCH "appointments/$($appointment.id)" $student.token @{status='Cancelled'} 200
    $available = Request 'Cancelled slot released' GET availability $student.token $null 200
    if (@($available).Count -ne 2) { throw 'Cancellation did not release slot.' }
    $null = Request 'Cannot reopen cancelled record' PATCH "appointments/$($appointment.id)" $student.token @{availabilityId=$slot1.id} 409
    $null = Request 'Rebook released slot' POST appointments $other.token @{availabilityId=$slot2.id} 201
    # Two requests at once must yield exactly one successful booking.
    $raceSlot = Request 'Create concurrent-test slot' POST availability $tutor.token (SlotBody 8 $tutor.user.id) 201
    $client = [System.Net.Http.HttpClient]::new()
    try {
        $pending = foreach ($token in @($student.token, $other.token)) {
            $message = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Post, "$base/appointments")
            $message.Headers.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $token)
            $message.Content = [System.Net.Http.StringContent]::new((@{availabilityId=$raceSlot.id} | ConvertTo-Json), [System.Text.Encoding]::UTF8, 'application/json')
            $client.SendAsync($message)
        }
        $responses = [System.Threading.Tasks.Task]::WhenAll([System.Threading.Tasks.Task[System.Net.Http.HttpResponseMessage][]]$pending).GetAwaiter().GetResult()
        $codes = @($responses | ForEach-Object { [int]$_.StatusCode } | Sort-Object)
        if (($codes -join ',') -ne '201,409') { throw "Concurrent booking failed: $codes" }
        $checks.Add('PASS Concurrent booking: one 201 and one 409')
    } finally { $client.Dispose() }
    $null = Request 'Deactivate user' DELETE "users/$($new.id)" $admin.token $null 200
    $remainingUsers = Request 'Deactivation preserves record' GET users $admin.token $null 200
    if (@($remainingUsers | Where-Object { $_.id -eq $new.id -and !$_.isActive }).Count -ne 1) { throw 'Deactivation removed record.' }
    $null = Request 'Inactive session rejected' GET appointments $other.token $null 401
    $null = Request 'Inactive login rejected' POST auth/login '' @{email='other@example.com';password='DemoPass123!'} 401
    $null = Request 'Logout' POST auth/logout $student.token $null 200
    $null = Request 'Logout invalidates token' GET appointments $student.token $null 401
    $null = Request 'Repeat logout unauthorized' POST auth/logout $student.token $null 401
    # Restart the same database and verify persistent accounts and sessions.
    $process.Kill($true); $process.WaitForExit()
    $stdout.GetAwaiter().GetResult() | Set-Content "$root/TestResults/server.log"
    $stderr.GetAwaiter().GetResult() | Set-Content "$root/TestResults/server-error.log"
    $process.Dispose()
    $process = [System.Diagnostics.Process]::Start($start)
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    $ready = $false
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        try { $null = Invoke-WebRequest "http://127.0.0.1:$Port/swagger/v1/swagger.json"; $ready = $true; break } catch { Start-Sleep -Milliseconds 250 }
    }
    if (!$ready) { throw 'Restart failed.' }
    $null = Request 'Session survives restart' GET users $admin.token $null 200
    $persisted = Request 'Appointment survives restart' GET "appointments/$($appointment.id)" $admin.token $null 200
    if ($persisted.Status -ne 'Cancelled') { throw 'Appointment status did not persist.' }
    $checks.Add("SUCCESS: $($checks.Count) HTTP checks passed.")
    $checks | Set-Content "$root/TestResults/verification.txt"
    $checks | Write-Output
} finally {
    if (!$process.HasExited) { $process.Kill($true); $process.WaitForExit() }
    $stdout.GetAwaiter().GetResult() | Add-Content "$root/TestResults/server.log"
    $stderr.GetAwaiter().GetResult() | Add-Content "$root/TestResults/server-error.log"
    $process.Dispose()
}
