using AcManager.Tools.Objects;
using StringBasedFilter;

namespace AcManager.Tools.Filters {
    public class TrackBaseObjectTester : ITester<TrackBaseObject> {
        public static TrackBaseObjectTester Instance = new TrackBaseObjectTester();

        public static string InnerParameterFromKey(string key) {
            switch (key) {
                case "city":
                    return nameof(TrackBaseObject.City);

                case "geotags":
                    return nameof(TrackBaseObject.GeoTags);

                case "len":
                case "length":
                    return nameof(TrackBaseObject.SpecsLength);

                case "width":
                    return nameof(TrackBaseObject.SpecsWidth);

                case "pits":
                case "pitboxes":
                    return nameof(TrackBaseObject.SpecsPitboxes);
            }

            return null;
        }

        public string ParameterFromKey(string key) {
            return InnerParameterFromKey(key) ?? AcJsonObjectTester.InnerParameterFromKey(key);
        }

        public bool Test(TrackBaseObject obj, string key, ITestEntry value) {
            switch (key) {
                case "city":
                    return value.Test(obj.City);

                case "geotags":
                    return value.Test(obj.GeoTags?.ToString());

                case "len":
                case "length":
                    return  value.Test(obj.SpecsLength);

                case "width":
                    return value.Test(obj.SpecsWidth);

                case "pits":
                case "pitboxes":
                    return value.Test(obj.SpecsPitboxes);
            }

            return AcJsonObjectTester.Instance.Test(obj, key, value);
        }
    }
}