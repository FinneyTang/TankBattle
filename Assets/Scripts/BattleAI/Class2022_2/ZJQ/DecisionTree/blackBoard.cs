using System.Collections;
using System.Collections.Generic;
using ZJQ;

public class blackBoard {
    Dictionary<string, object> shareData;

    public blackBoard() {
        shareData = new Dictionary<string, object>();
    }

    public void addData(string key, object knowledge) {
        if (shareData.TryGetValue(key, out knowledge))
        {
            return;
        }
        else {
            shareData.Add(key, knowledge);
        }
    }

    public object getData(string key) {
        object knowledge;
        if (shareData.TryGetValue(key, out knowledge))
        {
            return knowledge;
        }

        return knowledge;
    }

    public void updateData(string key, object newKnow) {
        object knowledge;
        if (shareData.TryGetValue(key, out knowledge))
        {
            shareData[key] = newKnow;
        }
        else {
            shareData.Add(key, newKnow);
        }
    }

    public void cleanData() {
        shareData.Clear();
    }


}
