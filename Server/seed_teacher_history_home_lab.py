import os
import sys
import time

import paramiko


HOST = os.environ.get("LABSHIELD_SSH_HOST", "192.168.100.142")
USER = os.environ.get("LABSHIELD_SSH_USER", "carloserver")
PASSWORD = os.environ.get("LABSHIELD_SSH_PASSWORD")


CSV_DATA = """Siswa,Percobaan,APD Benar,APD Salah,Quiz Benar,Quiz Salah,Durasi APD (detik),Durasi Quiz (detik),Nilai,Nilai K3,Waktu Submit
amanda_azzahra,1,5,0,17,3,47.1,313.40500000000003,88,94,2026-05-26T04:41:18.857Z
shafa_adila,1,5,0,17,3,37.3,358.6664,88,94,2026-05-26T05:00:22.286Z
gendis_hafidzah,1,5,1,17,3,43.7,257.48769999999996,85,79,2026-05-26T04:46:34.286Z
nafis_ahmad,1,5,0,15,5,8.2,362.12519999999995,80,90,2026-05-26T04:53:48.000Z
Risda Safanurismaya,1,5,2,16,4,45.3,267.87379999999996,78,65,2026-05-26T04:58:24.000Z
hasna_idham,1,4,1,17,3,36.5,316.4380999999999,84,77,2026-05-26T04:47:53.143Z
kenaz_feivel,1,3,2,20,0,50.1,307.2268,92,66,2026-05-26T04:50:30.857Z
virel_aprilio,1,3,1,18,2,47.2,307.6157,88,76,2026-05-26T05:02:20.571Z
siti_hafizah,1,5,1,19,1,40.1,325.93240000000003,92,83,2026-05-26T05:01:01.714Z
larasati_larasati,1,5,2,19,1,15.8,293.008,89,71,2026-05-26T04:51:49.714Z
safira_ramadhani,1,3,1,20,0,17,254.7493,96,80,2026-05-26T04:59:42.857Z
rahel_amanda,1,4,0,15,5,28.1,267.1804,79,90,2026-05-26T04:57:05.143Z
farrel_ramadhan,1,3,2,17,3,16.1,290.54619999999994,80,60,2026-05-26T04:45:15.429Z
khairul_ardiansyah,1,4,0,17,3,15.9,294.4555999999999,88,94,2026-05-26T04:51:10.286Z
al_nugraha,1,5,1,19,1,47.7,350.73699999999997,92,83,2026-05-26T04:40:39.429Z
rizky_putra,1,5,0,19,1,12.5,324.3351,96,98,2026-05-26T04:59:03.429Z
adiva_alfariansyah,1,4,2,20,0,52.7,334.3429,92,70,2026-05-26T04:40:00.000Z
ismaliyah_ismaliyah,1,3,0,19,1,12.4,323.6528,96,98,2026-05-26T04:49:51.429Z
Nafisa.Lesta,1,3,1,15,5,34.2,322.1035,75,70,2026-05-26T04:54:27.429Z
muhammad_ayubi,1,3,0,18,2,8.1,278.4884,91,96,2026-05-26T04:53:08.571Z
rezki_ramadani,1,5,1,20,0,40.8,298.6329,96,85,2026-05-26T04:57:44.571Z
nazma_amelia,1,5,0,17,3,12.5,304.03610000000003,88,94,2026-05-26T04:55:06.857Z
azahra_kurnia,1,5,2,16,4,49.3,301.45000000000005,78,65,2026-05-26T04:42:37.714Z
IntanSyawal,1,4,2,19,0,49.29133605957031,201.899028301239,92,70,2026-05-26T04:49:12.000Z
anita_putri,1,3,0,19,1,44.8,229.8529,96,98,2026-05-26T04:41:58.286Z
fadhil_syahlevy,1,3,0,18,2,21,312.41799999999995,91,96,2026-05-26T04:44:36.000Z
evan_setiawan,1,4,2,17,3,40,253.56990000000002,81,64,2026-05-26T04:43:56.571Z
gina_malika,1,5,2,18,2,27.5,351.1707,85,69,2026-05-26T04:47:13.714Z
febra_kurniawan,1,5,2,20,0,49.5,261.75079999999997,93,73,2026-05-26T04:45:54.857Z
sitta_nadia,1,5,1,18,2,24.5,317.51259999999996,88,81,2026-05-26T05:01:41.143Z
yasmine_salsabila,1,5,0,15,5,11.3,252.1195,80,90,2026-05-26T05:03:00.000Z
nazwa_wijaya,1,3,1,16,4,39.5,296.3675,79,72,2026-05-26T04:55:46.286Z
dewi_melani,1,4,2,19,1,20.6,237.273,88,68,2026-05-26T04:43:17.143Z
hayfa_fiandika,1,4,2,19,1,31,302.5821,88,68,2026-05-26T04:48:32.571Z
putri_anggita,1,4,0,17,3,29.5,277.24140000000006,88,94,2026-05-26T04:56:25.714Z
muhamad_habibie,1,4,2,18,2,38.4,329.71430000000004,85,66,2026-05-26T04:52:29.143Z
"""


REMOTE_NODE_SCRIPT = r'''
const fs = require('fs');
const csv = fs.readFileSync('/tmp/labshield-teacher-history.csv', 'utf8').replace(/^\uFEFF/, '');
const lines = csv.trim().split(/\r?\n/);
const headers = lines.shift().split(',');

function parseLine(line) {
  const out = [];
  let cur = '';
  let quoted = false;

  for (let i = 0; i < line.length; i += 1) {
    const ch = line[i];
    if (ch === '"') {
      if (quoted && line[i + 1] === '"') {
        cur += '"';
        i += 1;
      } else {
        quoted = !quoted;
      }
    } else if (ch === ',' && !quoted) {
      out.push(cur);
      cur = '';
    } else {
      cur += ch;
    }
  }
  out.push(cur);
  return Object.fromEntries(headers.map((h, i) => [h, out[i] ?? '']));
}

const number = (value) => Number(value || 0);

function computeStandard(apdCorrect, apdWrong, quizCorrect, quizWrong) {
  const totalCorrect = apdCorrect + quizCorrect;
  const totalWrong = apdWrong + quizWrong;
  return totalCorrect + totalWrong > 0 ? Math.round((totalCorrect / (totalCorrect + totalWrong)) * 100) : 0;
}

function computeK3(apdCorrect, apdWrong, quizCorrect, quizWrong) {
  const apdAnswered = apdCorrect + apdWrong;
  const quizAnswered = quizCorrect + quizWrong;
  const apdAccuracy = apdAnswered > 0 ? (apdCorrect / apdAnswered) * 100 : 0;
  const quizAccuracy = quizAnswered > 0 ? (quizCorrect / quizAnswered) * 100 : 0;
  const weighted = (apdAccuracy * 0.6) + (quizAccuracy * 0.4) - Math.min(20, apdWrong * 5);
  return Math.max(0, Math.min(100, Math.round(weighted)));
}

const rows = lines.map(parseLine).map((row) => {
  const apdTotalCorrect = number(row['APD Benar']);
  const apdTotalWrong = number(row['APD Salah']);
  const quizTotalCorrect = number(row['Quiz Benar']);
  const quizTotalWrong = number(row['Quiz Salah']);
  const quizTimeTakenSeconds = number(row['Durasi Quiz (detik)']);
  const totalQuiz = quizTotalCorrect + quizTotalWrong;
  const finalScoreStandard = computeStandard(apdTotalCorrect, apdTotalWrong, quizTotalCorrect, quizTotalWrong);
  const finalScoreK3 = computeK3(apdTotalCorrect, apdTotalWrong, quizTotalCorrect, quizTotalWrong);

  return {
    studentName: row['Siswa'],
    attemptNumber: number(row['Percobaan']) || 1,
    apdTotalCorrect,
    apdTotalWrong,
    apdTimeTakenSeconds: number(row['Durasi APD (detik)']),
    quizTotalCorrect,
    quizTotalWrong,
    questionTimes: Array.from({ length: totalQuiz }, (_, index) => ({
      questionID: `Q${index + 1}`,
      timeTakenSeconds: totalQuiz > 0 ? quizTimeTakenSeconds / totalQuiz : 0,
      isCorrect: index < quizTotalCorrect
    })),
    quizTimeTakenSeconds,
    finalScore: finalScoreStandard,
    finalScoreStandard,
    finalScoreK3,
    timestamp: row['Waktu Submit']
  };
});

const scoresPath = '/app/data/student_scores.json';
const backupPath = `/app/data/student_scores.backup-${Date.now()}.json`;
if (fs.existsSync(scoresPath)) {
  fs.copyFileSync(scoresPath, backupPath);
}
fs.writeFileSync(scoresPath, JSON.stringify(rows, null, 2));
console.log(JSON.stringify({ written: rows.length, backupPath, first: rows[0].studentName, last: rows[rows.length - 1].studentName }, null, 2));
'''


def connect():
    if not PASSWORD:
        raise RuntimeError("Set LABSHIELD_SSH_PASSWORD before running this script.")

    last_error = None
    for _ in range(3):
        try:
            client = paramiko.SSHClient()
            client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
            client.connect(
                hostname=HOST,
                username=USER,
                password=PASSWORD,
                timeout=20,
                auth_timeout=20,
                banner_timeout=20,
                look_for_keys=False,
                allow_agent=False,
            )
            return client
        except Exception as exc:
            last_error = exc
            time.sleep(2)

    raise last_error


def exec_checked(client, command, timeout=120):
    stdin, stdout, stderr = client.exec_command(command, timeout=timeout)
    out = stdout.read().decode(errors="replace")
    err = stderr.read().decode(errors="replace")
    code = stdout.channel.recv_exit_status()
    if out:
        print(out, end="")
    if err:
        print(err, end="", file=sys.stderr)
    if code != 0:
        raise RuntimeError(f"Remote command failed with exit code {code}")


def main():
    client = connect()
    try:
        sftp = client.open_sftp()
        try:
            with sftp.file('/tmp/labshield-teacher-history.csv', 'w') as remote_file:
                remote_file.write(CSV_DATA)
            with sftp.file('/tmp/labshield-seed-teacher-history.js', 'w') as remote_file:
                remote_file.write(REMOTE_NODE_SCRIPT)
        finally:
            sftp.close()

        exec_checked(client, "docker cp /tmp/labshield-teacher-history.csv labshield-server:/tmp/labshield-teacher-history.csv")
        exec_checked(client, "docker cp /tmp/labshield-seed-teacher-history.js labshield-server:/tmp/labshield-seed-teacher-history.js")
        exec_checked(client, "docker exec labshield-server node /tmp/labshield-seed-teacher-history.js")
    finally:
        client.close()


if __name__ == "__main__":
    main()
