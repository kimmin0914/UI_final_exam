using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ACTMULTILIB_K;

namespace PLCTest5
{
    public partial class Form1 : Form
    {
        // PLC 통신을 담당하는 인터페이스 객체 생성
        ActEasyIF control = new ActEasyIF();

        // [제어용 전역 변수 설정]
        bool isConnected = false;  // PLC 연결 성공 여부 플래그
        bool isAutoMode = false;   // 자동 운전 모드 활성화 플래그 (true: 자동, false: 수동)
        int step = 0;              // 유한 상태 머신(FSM)의 현재 공정 단계(Step) 번호
        short y_value = 0;         // PLC Y접점(출력)에 보낼 16비트 데이터 버퍼
        int delayCount = 0;        // 3D 시뮬레이터의 물리적 구동 시간을 맞추기 위한 카운터 변수

        // =================================================================
        // 1. 입력(X) 센서 주소 정의 (비트 쉬프트 연산 활용)
        // =================================================================
        // 1 << n 은 n번째 비트만 1로 만들고 나머지는 0으로 만드는 비트 연산입니다.
        int SENSOR_B_FWD = 1 << 2;  // X02 접점: B실린더 전진 완료 센서
        int SENSOR_B_BWD = 1 << 3;  // X03 접점: B실린더 후진 완료 센서
        int SENSOR_C_BWD = 1 << 4;  // X04 접점: C실린더 후진 완료 센서
        int SENSOR_C_FWD = 1 << 5;  // X05 접점: C실린더 전진 완료 센서
        int STAGE_A = 1 << 10; // X0A 접점: A리프트 상단 물건 감지 센서
        int STAGE_B = 1 << 11; // X0B 접점: B리프트 상단 물건 감지 센서

        // =================================================================
        // 2. 출력(Y) 명령 주소 정의 (비트 쉬프트 연산 활용)
        // =================================================================
        int OUT_B_FWD = 1 << 1;    // Y01 접점: B실린더 전진 명령
        int OUT_B_BWD = 1 << 2;    // Y02 접점: B실린더 후진 명령
        int OUT_C_FWD = 1 << 3;    // Y03 접점: C실린더 전진 명령
        int OUT_C_BWD = 1 << 4;    // Y04 접점: C실린더 후진 명령
        int OUT_A_UP = 1 << 5;    // Y05 접점: A리프트 상승 명령
        int OUT_A_DOWN = 1 << 6;    // Y06 접점: A리프트 하강 명령
        int OUT_B_DOWN = 1 << 7;    // Y07 접점: B리프트 하강 명령
        int OUT_B_UP = 1 << 8;    // Y08 접점: B리프트 상승 명령

        public Form1()
        {
            InitializeComponent();

            // 디자이너 파일 편집으로 인해 발생할 수 있는 타이머 이벤트 연결 끊김 현상 방지
            this.timer1.Tick -= new System.EventHandler(this.timer1_Tick);
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);

            // 프로그램 기동 시 화이트톤 테마 레이아웃 디자인 동적 적용
            ApplyCleanWhiteDesign();
        }

        // =================================================================
        // 🎨 GUI 디자인 세팅 함수 (스마트 팩토리 표준 클린 화이트 테마)
        // =================================================================
        private void ApplyCleanWhiteDesign()
        {
            // 메인 폼 배경색 설정 (눈이 편안한 파스텔 회백색)
            this.BackColor = Color.FromArgb(245, 247, 250);

            // 전광판 라벨 스타일 세팅 (흰색 배경에 어두운 스틸톤 글자)
            label1.BackColor = Color.White;
            label1.ForeColor = Color.FromArgb(44, 62, 80);
            label1.Font = new Font("맑은 고딕", 11, FontStyle.Bold);
            label1.BorderStyle = BorderStyle.FixedSingle;

            // 긴 공정 메시지가 출력되어도 글자가 잘리지 않도록 크기 자동화 및 내측 여백 부여
            label1.AutoSize = true;
            label1.Padding = new Padding(15, 10, 15, 10);
            label1.TextAlign = ContentAlignment.MiddleCenter;

            // 폼 내의 모든 컨트롤을 탐색하여 버튼 스타일 일괄 적용
            foreach (Control ctrl in this.Controls)
            {
                if (ctrl is Button btn)
                {
                    btn.FlatStyle = FlatStyle.Flat; // 3D 입체 효과 제거 (Flat 디자인)
                    btn.FlatAppearance.BorderSize = 1; // 테두리 두께
                    btn.FlatAppearance.BorderColor = Color.FromArgb(220, 224, 230); // 연한 그레이 테두리
                    btn.Font = new Font("맑은 고딕", 10, FontStyle.Bold);
                    btn.Cursor = Cursors.Hand; // 마우스 오버 시 손가락 커서 변환

                    // 버튼 이름(ID)별 조건 분기를 통한 파스텔 직관적 컬러 부여
                    if (btn.Name == "button1") // [연결] 버튼
                    {
                        btn.BackColor = Color.FromArgb(230, 242, 255); // 소프트 블루
                        btn.ForeColor = Color.FromArgb(0, 102, 204);
                    }
                    else if (btn.Name == "button2") // [시작] 버튼
                    {
                        btn.BackColor = Color.FromArgb(235, 247, 238); // 소프트 그린
                        btn.ForeColor = Color.FromArgb(39, 138, 59);
                    }
                    else if (btn.Name == "button3") // [정지] 버튼
                    {
                        btn.BackColor = Color.FromArgb(253, 238, 238); // 소프트 레드
                        btn.ForeColor = Color.FromArgb(204, 51, 51);
                    }
                    else // 수동 조작용 개별 버튼 8개
                    {
                        btn.BackColor = Color.White; // 순백색 배경
                        btn.ForeColor = Color.FromArgb(70, 80, 90);
                        // 마우스 반응 애니메이션 효과 효과 적용
                        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(240, 242, 245);
                        btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(225, 230, 235);
                    }
                }
            }
        }

        // =================================================================
        // 3. 마스터 제어 및 개별 수동 버튼 인터페이스
        // =================================================================

        // [button1] PLC 통신 연결 버튼
        private void button1_Click(object sender, EventArgs e)
        {
            if (control.Open() == 0) // Open() 반환값이 0이면 통신 연결 성공
            {
                MessageBox.Show("연결 성공!");
                isConnected = true;
                timer1.Interval = 100; // 타이머 주기를 0.1초(100ms)로 세팅 (센서 폴링)
                timer1.Enabled = true; // 스캔 타이머 가동 시작
                label1.Text = "연결됨. [시작] 버튼을 누르세요.";
            }
        }

        // [button2] 자동화 공정 시작 버튼
        private void button2_Click(object sender, EventArgs e)
        {
            if (!isConnected) return; // PLC 미연결 시 실행 거부
            isAutoMode = true; // 자동 루프 가동 플래그 ON
            step = 0;          // 시퀀스 단계를 초기화
            delayCount = 0;

            // 비트 논리합(|)을 통해 초기 자세 제어 (B,C실린더 후진 유지 / A리프트 상단 대기 / B리프트 하단 대기)
            y_value = (short)(y_value | OUT_B_BWD | OUT_C_BWD | OUT_A_UP | OUT_B_DOWN);
            // 비트 논리곱 및 보수(& ~) 연산으로 충돌 소지가 있는 반대 출력 비트는 철저히 초기화(OFF)
            y_value = (short)(y_value & ~OUT_B_FWD & ~OUT_C_FWD & ~OUT_A_DOWN & ~OUT_B_UP);
            control.WriteDeviceBlock2("Y0", 1, ref y_value); // 연산 버퍼를 PLC 실제 Y접점에 전송

            label1.Text = "[자동] 대기 중... 물건을 올려주세요.";
        }

        // [button3] 자동화 공정 비상 정지 버튼
        private void button3_Click(object sender, EventArgs e)
        {
            isAutoMode = false; // 자동 루프 즉시 차단
            step = 0;
            y_value = 0; // 모든 출력을 안전하게 OFF(0) 상태로 초기화
            control.WriteDeviceBlock2("Y0", 1, ref y_value);
            label1.Text = "자동운전 정지됨.";
        }

        // 수동 제어 로직 (버튼 4번 ~ 11번): 상호 배타적 동작 간섭 방지를 위해 비트 마스킹 적용
        private void button4_Click(object sender, EventArgs e)
        {
            isAutoMode = false; // 수동 개입 시 자동 모드 안전 해제
            y_value = (short)(y_value | OUT_B_FWD); y_value = (short)(y_value & ~OUT_B_BWD); control.WriteDeviceBlock2("Y0", 1, ref y_value);
            label1.Text = "수동: B실린더 전진";
        }
        private void button5_Click(object sender, EventArgs e)
        {
            isAutoMode = false; y_value = (short)(y_value & ~OUT_B_FWD); y_value = (short)(y_value | OUT_B_BWD); control.WriteDeviceBlock2("Y0", 1, ref y_value);
            label1.Text = "수동: B실린더 후진";
        }
        private void button6_Click(object sender, EventArgs e)
        {
            isAutoMode = false; y_value = (short)(y_value | OUT_B_UP); y_value = (short)(y_value & ~OUT_B_DOWN); control.WriteDeviceBlock2("Y0", 1, ref y_value);
            label1.Text = "수동: B리프트 상승(UP)";
        }
        private void button7_Click(object sender, EventArgs e)
        {
            isAutoMode = false; y_value = (short)(y_value & ~OUT_B_UP); y_value = (short)(y_value | OUT_B_DOWN); control.WriteDeviceBlock2("Y0", 1, ref y_value);
            label1.Text = "수동: B리프트 하강(DOWN)";
        }
        private void button8_Click(object sender, EventArgs e)
        {
            isAutoMode = false; y_value = (short)(y_value | OUT_C_FWD); y_value = (short)(y_value & ~OUT_C_BWD); control.WriteDeviceBlock2("Y0", 1, ref y_value);
            label1.Text = "수동: C실린더 전진";
        }
        private void button9_Click(object sender, EventArgs e)
        {
            isAutoMode = false; y_value = (short)(y_value & ~OUT_C_FWD); y_value = (short)(y_value | OUT_C_BWD); control.WriteDeviceBlock2("Y0", 1, ref y_value);
            label1.Text = "수동: C실린더 후진";
        }
        private void button10_Click(object sender, EventArgs e)
        {
            isAutoMode = false; y_value = (short)(y_value | OUT_A_UP); y_value = (short)(y_value & ~OUT_A_DOWN); control.WriteDeviceBlock2("Y0", 1, ref y_value);
            label1.Text = "수동: A리프트 상승(UP)";
        }
        private void button11_Click(object sender, EventArgs e)
        {
            isAutoMode = false; y_value = (short)(y_value & ~OUT_A_UP); y_value = (short)(y_value | OUT_A_DOWN); control.WriteDeviceBlock2("Y0", 1, ref y_value);
            label1.Text = "수동: A리프트 하강(DOWN)";
        }

        private void label1_Click(object sender, EventArgs e) { }

        // =================================================================
        // 4. 타이머 틱 이벤트: 상태 머신(FSM) 기반 동시 구동 자동화 시퀀스 엔진
        // =================================================================
        private void timer1_Tick(object sender, EventArgs e)
        {
            // 통신 연결이 끊겼거나 자동운전 플래그가 비활성화 상태라면 즉시 연산 스킵
            if (!isConnected || !isAutoMode) return;

            short sensor = 0;
            // X접점(입력 센서 데이터)을 1워드(16비트) 단위로 읽어와 정수형 버퍼(sensor)에 저장
            if (control.ReadDeviceBlock2("X0", 1, out sensor) != 0) return;

            // switch문을 사용한 상태 머신 구조. 현재 step 값에 매칭되는 독립 공정만 실행
            switch (step)
            {
                case 0: // [상단 대기 레이어 동작 정의]
                    label1.Text = "[0단계] A리프트 대기 중... (물건 감지 시 출발)";

                    // 비트 AND 연산(&)을 통해 A리프트에 물건이 올려졌는지(STAGE_A) 확인
                    if ((sensor & STAGE_A) != 0)
                    {
                        // 택트 타임(Tact Time) 단축을 위해 B리프트 상승과 B실린더 전진 명령을 한 접점에 동시 인가
                        y_value = (short)(y_value | OUT_B_UP | OUT_B_FWD);
                        y_value = (short)(y_value & ~OUT_B_DOWN & ~OUT_B_BWD);
                        control.WriteDeviceBlock2("Y0", 1, ref y_value);

                        step = 1; // 조건 충족으로 인해 다음 공정 단계로 전이
                        delayCount = 0; // 사용이 끝난 딜레이 카운트 변수 클리어
                    }
                    break;

                case 1: // [상단 수평 이송 및 실린더 안정화 단계]
                    label1.Text = "[1단계] B리프트 상승 & B실린더 전진 중...";

                    // B실린더가 물건을 끝까지 밀어내어 전진 완료 센서(SENSOR_B_FWD)를 작동시켰는지 감시
                    if ((sensor & SENSOR_B_FWD) != 0)
                    {
                        delayCount++; // 3D 물리 엔진 상 물건이 통통 튀는 기구부 관성 안정화 시간 확보 (0.1초씩 누적)
                        if (delayCount >= 5) // 0.5초 경과 시 수행
                        {
                            // 전진 출력을 끊고 안전하게 실린더를 후진 기동시킴
                            y_value = (short)(y_value & ~OUT_B_FWD);
                            y_value = (short)(y_value | OUT_B_BWD);
                            control.WriteDeviceBlock2("Y0", 1, ref y_value);

                            step = 2; // 다음 상태 전이
                            delayCount = 0;
                        }
                    }
                    break;

                case 2: // [B리프트 상단 안착 검증 및 수직 하강 지령]
                    label1.Text = "[2단계] B실린더 후진 & 물건 안착 대기...";

                    // 조건: B실린더가 완벽히 후진 완료하고(&) 물건이 B리프트 탑층 센서(STAGE_B)에 정상 검증되었을 때
                    if (((sensor & SENSOR_B_BWD) != 0) && ((sensor & STAGE_B) != 0))
                    {
                        delayCount++;
                        if (delayCount >= 5) // 안착 후 안전 여유 마진 (0.5초)
                        {
                            // 물류 하강 레이어로 이송하기 위해 B리프트 하강 명령 하달.
                            // 이때 하단에서 마중 나와 물건을 안전하게 토스받을 수 있게 A리프트도 선제적 하강 동시 기동.
                            y_value = (short)(y_value & ~OUT_B_UP);
                            y_value = (short)(y_value | OUT_B_DOWN);
                            y_value = (short)(y_value & ~OUT_A_UP);
                            y_value = (short)(y_value | OUT_A_DOWN);
                            control.WriteDeviceBlock2("Y0", 1, ref y_value);

                            step = 3;
                            delayCount = 0;
                        }
                    }
                    else delayCount = 0; // 센서가 중간에 유실될 경우 타이머 리셋 예외처리
                    break;

                case 3: // [하단 이송을 위한 하강 기구부 동기화 대기 단계]
                    label1.Text = "[3단계] 리프트 하강 대기 중 (충돌 방지)";
                    delayCount++; // 논블로킹 방식의 카운터 축적 알고리즘

                    // 소프트웨어 연산 속도 대비 하드웨어 리프트 구동 물리 속도가 현저히 느리므로, 
                    // 두 기구부가 바닥층 테두리에 100% 안착할 수 있도록 약 3.0초(30카운트) 동안 강제 홀딩 제어 진행.
                    // 이 딜레이가 누적 루프 시 2회차 사이클부터 실린더가 리프트 옆통수를 때리는 결함을 영구 차단함.
                    if (delayCount >= 30)
                    {
                        // 두 리프트가 완전히 바닥에 안착하면 하단 밀어내기 용 C실린더 전진 출력 ON
                        y_value = (short)(y_value | OUT_C_FWD);
                        y_value = (short)(y_value & ~OUT_C_BWD);
                        control.WriteDeviceBlock2("Y0", 1, ref y_value);

                        step = 4;
                        delayCount = 0;
                    }
                    break;

                case 4: // [하단 수평 이송 및 C실린더 안전 복귀]
                    label1.Text = "[4단계] C실린더 전진 중...";

                    // C실린더 전진 궤도가 완료 점접(SENSOR_C_FWD)에 도달했는지 확인
                    if ((sensor & SENSOR_C_FWD) != 0)
                    {
                        delayCount++;
                        if (delayCount >= 5) // 밀어내기 작업 후 관성 흔들림 억제 딜레이 (0.5초)
                        {
                            // C실린더 복귀 지령 인가
                            y_value = (short)(y_value & ~OUT_C_FWD);
                            y_value = (short)(y_value | OUT_C_BWD);
                            control.WriteDeviceBlock2("Y0", 1, ref y_value);

                            step = 5;
                            delayCount = 0;
                        }
                    }
                    break;

                case 5: // [A리프트 안착 및 원상 복귀 수직 상승 단계]
                    label1.Text = "[5단계] C실린더 후진 & 물건 안착 대기...";

                    // C실린더가 안전선 안으로 후진 완료하고(&) 물건이 하단 A리프트 센서(STAGE_A)에 안정적으로 얹어졌는지 교차 검증
                    if (((sensor & SENSOR_C_BWD) != 0) && ((sensor & STAGE_A) != 0))
                    {
                        delayCount++;
                        if (delayCount >= 5) // 상승 가속도 충격 완화 대기 (0.5초)
                        {
                            // 물건을 상단 레이어로 복귀시키기 위해 A리프트 상승 제어권 기동
                            y_value = (short)(y_value | OUT_A_UP);
                            y_value = (short)(y_value & ~OUT_A_DOWN);
                            control.WriteDeviceBlock2("Y0", 1, ref y_value);

                            step = 6;
                            delayCount = 0;
                        }
                    }
                    else delayCount = 0;
                    break;

                case 6: // [최종 무한 루프 초기화 및 상태 머신 리셋 단계]
                    label1.Text = "[6단계] A리프트 상승 대기 중 (루프 준비)";
                    delayCount++;

                    // A리프트가 3D 가상 공간 상의 꼭대기 원점 좌표까지 안정적으로 올라갈 수 있도록 약 3.0초 시간 할당
                    if (delayCount >= 30)
                    {
                        step = 0; // 상태 번호를 다시 0으로 치환함으로써 영구적인 순환(Loop) 자동 제어 공정 완성
                        delayCount = 0;
                    }
                    break;
            }
        }
    }
}