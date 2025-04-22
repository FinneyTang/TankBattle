using Main;
using UnityEngine;
using UnityEngine.AI;
//没写完，有bug
namespace DMQ
{
    class MyTank : Tank
    {
        bool debugState = false;
        public float timeTotal = 180f;
        public float timeTurnPointGap = 7f;
        int timeTPFirstGet = 0;//0未达到，1接近超级星星，2超级星星结束
        float timeStart2Now;

        Tank tankMine;
        Tank tankEnemy;
        Vector3 lastPosEnemy = Vector3.zero;
        enum infstateSpEnum{
            sStarAndhLow,
            tOverAndhLow,
            superStar,
            timeOver,
            healthLow,
            scoreLow,
            nothing,
        }
        infstateSpEnum infState = infstateSpEnum.nothing;
        bool infstateChange = false;
        int starRemain = 0;
        bool gotoRecover = false;
        Vector3 goalposV3 = Vector3.zero;
        Missile missileFir;
        Missile missileSec;
        Missile missileThi;
        //int missileTotal = 0;
        public override string GetName(){return "DMQ";}
        protected override void OnStart()
        {
            tankMine = Match.instance.GetTank(Team);
            tankEnemy = Match.instance.GetOppositeTank(Team);
            lastPosEnemy = tankEnemy.Position;
            timeStart2Now = Time.time;
            
        }
        protected override void OnUpdate()
        {
            
            infMachine();
            if(debugState){
                Debug.Log(_GetInfState() + timeStart2Now);
            }
            moveMachine();
            attackMachine();
        }
        protected override void OnReborn()
        {
            timeStart2Now = Time.time;
        }
        
        private void moveMachine()
        {
            goalposV3 = starGoalPos();
            goalposV3 = healthGoalpOS();
            //刹车
            tankMine.Move(goalposV3);

        }

        private void attackMachine(){
            
            Vector3 enemyPosV3 = tankEnemy.Position;
            Vector3 vectormoveEnemy = enemyPosV3 - lastPosEnemy;

            tankMine.TurretTurnTo(enemyPosV3 + vectormoveEnemy * 20f);
            lastPosEnemy = enemyPosV3;
            if(debugState){
                Debug.DrawLine(enemyPosV3, enemyPosV3 + vectormoveEnemy * 20f);
            }
            if(!tankMine.CanSeeOthers(tankEnemy)){
                return;
            }
            tankMine.Fire();
        }
        private void attackAttach(){
            //暂时不写
            int missileCount = 0;
            foreach (var starListR in Match.instance.GetOppositeMissiles(tankMine.Team)){
                missileCount += 1;
                
            }
        }
        private void infMachine(){
            infstateSpEnum infstateNow = infstateSpEnum.nothing;
            //TimePart;
            timeStart2Now += Time.deltaTime;
            switch(timeTPFirstGet){
                case 0:
                    if(timeStart2Now > (timeTotal/2 - timeTurnPointGap)){timeTPFirstGet = 1;}
                    break;
                case 1:
                    if(timeStart2Now > timeTotal/2){timeTPFirstGet = 2;}else{infstateNow = infstateSpEnum.superStar;}
                    break;
                case 2:
                    if(timeStart2Now > timeTotal - timeTurnPointGap){infstateNow = infstateSpEnum.timeOver;}
                    break;
            }
            //HealthPart;
            if(tankMine.HP < 26){
                switch((int)infstateNow){
                    case(int)infstateSpEnum.superStar:
                        infstateNow = infstateSpEnum.sStarAndhLow;
                        break;
                    case(int)infstateSpEnum.timeOver:
                        infstateNow = infstateSpEnum.tOverAndhLow;
                        break;
                    default:
                        infstateNow = infstateSpEnum.healthLow;
                        break;
                }
            }
            //StatePart;新旧状态计算
            infstateChange = (infState != infstateNow)? true : false;
            infState = infstateNow;
        }
        private Vector3 starGoalPos(){
            if(infState == infstateSpEnum.superStar || infState == infstateSpEnum.sStarAndhLow){return new Vector3(0f, 0.5f, 0f);}
            Vector3 goalposV3I = Vector3.zero;
            int roundCount = 0;
            var starList = Match.instance.GetStars();
            Vector3 starFir = Vector3.zero;
            Vector3 starSec = Vector3.zero;
            Vector3 starThi = Vector3.zero;
            foreach (var starListR in Match.instance.GetStars()){
                if(!starListR.Value.IsSuperStar){
                    roundCount ++;
                    switch(roundCount){
                        case 1:
                            starFir = starListR.Value.Position;
                            break;
                        case 2:
                            starSec = starListR.Value.Position;
                            break;
                        case 3:
                            starThi = starListR.Value.Position;
                            break;    
                    }

                }
            }

            if(starRemain == (roundCount)){return goalposV3;}else{starRemain = roundCount;}

            switch(roundCount){
                case 0:
                    goalposV3I = new Vector3(0,0.5f,0);
                    break;
                case 1:
                    goalposV3I = starFir;
                    break;
                case 2:
                    goalposV3I = _DisTD(tankMine.Position, starFir, starSec);
                    break;
                case 3:
                    float dis12 = _DisTD(1, starFir, starSec);
                    float dis13 = _DisTD(1, starFir, starThi);
                    float dis23 = _DisTD(1, starSec, starThi);
                    if(dis12 < dis13){
                        if(dis12 < dis23){goalposV3I = _DisTD(tankMine.Position, starFir, starSec);
                        }else if(dis13 < dis23){goalposV3I = _DisTD(tankMine.Position, starFir, starThi);
                        }else{goalposV3I = _DisTD(tankMine.Position, starSec, starThi);}
                    }else{
                        if(dis13 < dis23){goalposV3I = _DisTD(tankMine.Position, starFir, starThi);
                        }else{goalposV3I = _DisTD(tankMine.Position, starSec, starThi);}
                    }
                    break;
            }

            return goalposV3I;
        }
        private Vector3 healthGoalpOS(){
            if(infState == infstateSpEnum.superStar || infState == infstateSpEnum.sStarAndhLow){return goalposV3;}
            if(gotoRecover){
                if(tankMine.HP > 75){
                    gotoRecover = false;
                    return goalposV3;
                }else{
                    return Match.instance.GetRebornPos(tankMine.Team);
                }
            }
            float hpRecoverPt = 0f;
            int myhealthI = tankMine.HP;
            myhealthI = (myhealthI == 100)? 150 : myhealthI;  
            int gaphealthI = tankEnemy.HP - tankMine.HP;
            if(tankEnemy.IsDead){gaphealthI = -2 - tankMine.HP;}else{gaphealthI = tankEnemy.HP - tankMine.HP;}
            while(myhealthI > 25){
                myhealthI -= 25;
                hpRecoverPt ++ ;
            }
            if(gaphealthI >= 0){
                while(gaphealthI > 25){
                gaphealthI -= 25;
                hpRecoverPt -- ;
                }
            }else{
                gaphealthI = gaphealthI * -1;
                while(gaphealthI > 25){
                gaphealthI -= 25;
                hpRecoverPt ++ ;
                }
            }
            hpRecoverPt = hpRecoverPt + (float)starRemain - 1;
            if(hpRecoverPt < 1){
                gotoRecover = true;
                return Match.instance.GetRebornPos(tankMine.Team);
            }else{
                return goalposV3;
            }
        }

        //_DisTD
        //第一个计算pos1、pos2到pos0距离并给出其中离pos1较近的V3；
        //第二个计算pos1、pos2之间的距离，calMode为1仅用于比大小，为2精确计算；
        private Vector3 _DisTD(Vector3 pos0, Vector3 pos1, Vector3 pos2){
            return (_DisTD(1, pos0 ,pos1) < _DisTD(1, pos0, pos2))? pos1 : pos2;
        }
        private float _DisTD(int calMode, Vector3 Pos1, Vector3 Pos2){
            float returnFloat = 0;
            switch(calMode){
                case 1:
                    returnFloat = (Pos1 - Pos2).sqrMagnitude;
                    break;
                case 2:
                    returnFloat = Vector3.Distance(Pos1, Pos2);
                    break;
            }
            return returnFloat;
        }
        private string _GetInfState(){
            switch((int)infState){
                case (int)infstateSpEnum.healthLow:
                    return "healthlow";
                case (int)infstateSpEnum.nothing:
                    return "nothing";
                case (int)infstateSpEnum.scoreLow:
                    return "scoreLow";
                case (int)infstateSpEnum.sStarAndhLow:
                    return "sStarAndhLow";
                case (int)infstateSpEnum.superStar:
                    return "superStar";
                case (int)infstateSpEnum.timeOver:
                    return "timeOver";
                case (int)infstateSpEnum.tOverAndhLow:
                    return "tOverAndhLow";
            }
            return "redrum";
        }
    }
}
